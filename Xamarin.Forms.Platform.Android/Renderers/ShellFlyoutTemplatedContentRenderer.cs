using Android.Content;
using Android.Graphics.Drawables;
using Android.Runtime;
using Android.Support.Design.Widget;
using Android.Support.V7.Widget;
using Android.Util;
using Android.Views;
using Android.Widget;
using System;
using System.ComponentModel;
using AView = Android.Views.View;
using LP = Android.Views.ViewGroup.LayoutParams;
using Android.Graphics;

namespace Xamarin.Forms.Platform.Android
{
    public class ShellFlyoutTemplatedContentRenderer : Java.Lang.Object, IShellFlyoutContentRenderer
		, AppBarLayout.IOnOffsetChangedListener
    {
        #region IShellFlyoutContentRenderer

        public AView AndroidView => _rootView;

        #endregion IShellFlyoutContentRenderer

        IShellContext _shellContext;
        bool _disposed;
        HeaderContainer _headerView;
        AView _rootView;
        Drawable _defaultBackground;
		ImageView _bgImage;

		public ShellFlyoutTemplatedContentRenderer(IShellContext shellContext)
        {
            _shellContext = shellContext;

            LoadView(shellContext);
        }

        protected virtual void LoadView(IShellContext shellContext)
        {
            var context = shellContext.AndroidContext;
            var coordinator = LayoutInflater.FromContext(context).Inflate(Resource.Layout.FlyoutContent, null);
            var recycler = coordinator.FindViewById<RecyclerView>(Resource.Id.flyoutcontent_recycler);
            var appBar = coordinator.FindViewById<AppBarLayout>(Resource.Id.flyoutcontent_appbar);

            _rootView = coordinator;

            appBar.AddOnOffsetChangedListener(this);

            int actionBarHeight = (int)context.ToPixels(56);

            _headerView = new HeaderContainer(context, ((IShellController)shellContext.Shell).FlyoutHeader)
            {
                MatchWidth = true
            };
            _headerView.SetMinimumHeight(actionBarHeight);
            _headerView.LayoutParameters = new AppBarLayout.LayoutParams(LP.MatchParent, LP.WrapContent)
            {
                ScrollFlags = AppBarLayout.LayoutParams.ScrollFlagScroll
            };
            appBar.AddView(_headerView);

            var adapter = new ShellFlyoutRecyclerAdapter(shellContext, OnElementSelected);
            recycler.SetPadding(0, (int)context.ToPixels(20), 0, 0);
            recycler.SetClipToPadding(false);
            recycler.SetLayoutManager(new LinearLayoutManager(context, (int)Orientation.Vertical, false));
            recycler.SetAdapter(adapter);

            var metrics = context.Resources.DisplayMetrics;
            var width = Math.Min(metrics.WidthPixels, metrics.HeightPixels);

            TypedValue tv = new TypedValue();
            if (context.Theme.ResolveAttribute(global::Android.Resource.Attribute.ActionBarSize, tv, true))
            {
                actionBarHeight = TypedValue.ComplexToDimensionPixelSize(tv.Data, metrics);
            }
            width -= actionBarHeight;

            coordinator.LayoutParameters = new LP(width, LP.MatchParent);

			_bgImage = new ImageView(context)
			{
				LayoutParameters = new LP(coordinator.LayoutParameters),
				Elevation = -100
			};

			UpdateFlyoutHeaderBehavior();
            _shellContext.Shell.PropertyChanged += OnShellPropertyChanged;

            UpdateFlyoutBackground();
        }

        protected void OnElementSelected(Element element)
        {
            ((IShellController)_shellContext.Shell).OnFlyoutItemSelected(element);
        }

        protected virtual void OnShellPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == Shell.FlyoutHeaderBehaviorProperty.PropertyName)
                UpdateFlyoutHeaderBehavior();
            else if (e.IsOneOf(
				Shell.FlyoutBackgroundColorProperty, 
				Shell.FlyoutBackgroundImageProperty, 
				Shell.FlyoutBackgroundImageAspectProperty))
                UpdateFlyoutBackground();
        }

		//Size AspectFit(Size aspectSize, Size boundingSize)
		//{
		//	var width = boundingSize.Width;
		//	var height = boundingSize.Height;
		//	var mW = boundingSize.Width / aspectSize.Width;
		//	var mH = boundingSize.Height / aspectSize.Height;
		//	if (mH < mW)
		//		width = boundingSize.Height * aspectSize.Width / aspectSize.Height;
		//	else if (mW < mH)
		//		height = boundingSize.Width * aspectSize.Height / aspectSize.Width;

		//	return new Size(width, height);
		//}

		//Size AspectFill(Size aspectSize, Size minimumSize)
		//{
		//	var width = minimumSize.Width;
		//	var height = minimumSize.Height;
		//	var mW = minimumSize.Width / aspectSize.Width;
		//	var mH = minimumSize.Height / aspectSize.Height;
		//	if (mH > mW)
		//		width = minimumSize.Height * aspectSize.Width / aspectSize.Height;
		//	else if (mW > mH)
		//		height = minimumSize.Width * aspectSize.Height / aspectSize.Width;

		//	return new Size(width, height);
		//}

		//class AspectDrawable: Drawable
		//{
		//	Drawable _target;

		//	public AspectDrawable(Drawable target)
		//	{
		//		_target = target;
		//	}

		//	public override void SetBounds(int left, int top, int right, int bottom)
		//	{
		//		var sourceRect = new RectF(0, 0, _target.IntrinsicWidth, _target.IntrinsicHeight);
		//		var screenRect = new RectF(left, top, right, bottom);

		//		var matrix = new Matrix();
		//		matrix.SetRectToRect(screenRect, sourceRect, Matrix.ScaleToFit.Center);

		//		var inverse = new Matrix();
		//		matrix.Invert(inverse);
		//		inverse.MapRect(sourceRect);

		//		_target.SetBounds((int)sourceRect.Left, (int)sourceRect.Top, (int)sourceRect.Right, (int)sourceRect.Bottom);

		//		base.SetBounds(left, top, right, bottom);
		//	}

		//	protected override void Dispose(bool disposing)
		//	{
		//		if (_target != null && !_target.IsDisposed())
		//			_target.Dispose();
		//		base.Dispose(disposing);
		//	}

		//	public override int Opacity => _target.Opacity;

		//	public override void Draw(Canvas canvas)
		//	{
		//		canvas.Save();
		//		canvas.ClipRect(Bounds);
		//		_target.Draw(canvas);
		//		canvas.Restore();
		//	}

		//	public override void SetAlpha(int alpha) => _target.SetAlpha(alpha);

		//	public override void SetColorFilter(ColorFilter colorFilter) => _target.SetColorFilter(colorFilter);
		//}

		protected virtual async void UpdateFlyoutBackground()
		{
			var color = _shellContext.Shell.FlyoutBackgroundColor;
			var imageSource = _shellContext.Shell.FlyoutBackgroundImage;
			if (_defaultBackground == null && color.IsDefault && !_shellContext.Shell.IsSet(Shell.FlyoutBackgroundImageProperty))
				return;

			if (_defaultBackground == null)
				_defaultBackground = _rootView.Background;

			_rootView.Background = color.IsDefault ? _defaultBackground : new ColorDrawable(color.ToAndroid());

			if (imageSource == null)
			{
				if (_rootView is ViewGroup view && view.IndexOfChild(_bgImage) == -1)
					view.RemoveView(_bgImage);
				return;
			}

			using (var drawable = await _shellContext.AndroidContext.GetFormsDrawableAsync(imageSource) as BitmapDrawable)
			{
				if (_rootView.IsDisposed() || drawable == null || !(_rootView is ViewGroup view))
					return;

				if (view.IndexOfChild(_bgImage) == -1)
					view.AddView(_bgImage);

				var bitmapSize = new Size(drawable.Bitmap.Width, drawable.Bitmap.Height);
				var boundingSize = new Size(_rootView.Width, _rootView.Height - _headerView.Height);
				var size = bitmapSize;

				_bgImage.SetImageDrawable(drawable);

				// TODO
				switch (_shellContext.Shell.FlyoutBackgroundImageAspect)
				{
					default:
					case Aspect.AspectFit:
						_bgImage.SetScaleType(ImageView.ScaleType.Center);
						//size = AspectFit(bitmapSize, boundingSize);
						//drawable.Gravity = GravityFlags.Center;
						break;
					case Aspect.AspectFill:
						_bgImage.SetScaleType(ImageView.ScaleType.FitCenter);
						//size = AspectFill(bitmapSize, boundingSize);
						//drawable.Gravity = GravityFlags.RelativeLayoutDirection;
						break;
					case Aspect.Fill:
						_bgImage.SetScaleType(ImageView.ScaleType.FitXy);
						//drawable.Gravity = GravityFlags.Fill;
						break;
				}
				//var paddingW = (boundingSize.Width - size.Width) / 2;
				//var paddingH = (boundingSize.Height - size.Height) / 2;


				////var ad = new AspectDrawable(drawable);

				//drawable.SetBounds(
				//	(int)(boundingSize.Width + paddingW),
				//	(int)(boundingSize.Height + paddingH),
				//	(int)(size.Width - paddingW),
				//	(int)(size.Height - paddingH));

				//_rootView.Background = new LayerDrawable(new[] { _rootView.Background, drawable });
			}
		}

        protected virtual void UpdateFlyoutHeaderBehavior()
        {
            switch (_shellContext.Shell.FlyoutHeaderBehavior)
            {
                case FlyoutHeaderBehavior.Default:
                case FlyoutHeaderBehavior.Fixed:
                    _headerView.LayoutParameters = new AppBarLayout.LayoutParams(LP.MatchParent, LP.WrapContent)
                    {
                        ScrollFlags = 0
                    };
                    break;
                case FlyoutHeaderBehavior.Scroll:
                    _headerView.LayoutParameters = new AppBarLayout.LayoutParams(LP.MatchParent, LP.WrapContent)
                    {
                        ScrollFlags = AppBarLayout.LayoutParams.ScrollFlagScroll
                    };
                    break;
                case FlyoutHeaderBehavior.CollapseOnScroll:
                    _headerView.LayoutParameters = new AppBarLayout.LayoutParams(LP.MatchParent, LP.WrapContent)
                    {
                        ScrollFlags = AppBarLayout.LayoutParams.ScrollFlagExitUntilCollapsed |
                            AppBarLayout.LayoutParams.ScrollFlagScroll
                    };
                    break;
            }
        }

        public void OnOffsetChanged(AppBarLayout appBarLayout, int verticalOffset)
        {
            var headerBehavior = _shellContext.Shell.FlyoutHeaderBehavior;
            if (headerBehavior != FlyoutHeaderBehavior.CollapseOnScroll)
                return;

            _headerView.SetPadding(0, -verticalOffset, 0, 0);
        }

        protected override void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _shellContext.Shell.PropertyChanged -= OnShellPropertyChanged;
                    _headerView.Dispose();
                    _rootView.Dispose();
                    _defaultBackground?.Dispose();
					_bgImage?.Dispose();
				}

                _defaultBackground = null;
				_bgImage = null;
				_rootView = null;
                _headerView = null;
                _shellContext = null;
                _disposed = true;
            }

            base.Dispose(disposing);
        }

        // This view lets us use the top padding to "squish" the content down
        public class HeaderContainer : ContainerView
        {
            public HeaderContainer(Context context, View view) : base(context, view)
            {
            }

            public HeaderContainer(Context context, IAttributeSet attribs) : base(context, attribs)
            {
            }

            public HeaderContainer(Context context, IAttributeSet attribs, int defStyleAttr) : base(context, attribs, defStyleAttr)
            {
            }

            protected HeaderContainer(IntPtr javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
            {
            }

            protected override void LayoutView(double x, double y, double width, double height)
            {
                var context = Context;
                var paddingLeft = context.FromPixels(PaddingLeft);
                var paddingTop = context.FromPixels(PaddingTop);
                var paddingRight = context.FromPixels(PaddingRight);
                var paddingBottom = context.FromPixels(PaddingBottom);

                width -= paddingLeft + paddingRight;
                height -= paddingTop + paddingBottom;

                View.Layout(new Rectangle(paddingLeft, paddingTop, width, height));
            }
        }
    }
}