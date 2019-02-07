﻿using System;
using Foundation;
using UIKit;

namespace Xamarin.Forms.Platform.iOS
{
	// TODO hartez 2018/06/01 14:17:00 Implement Dispose override ?	
	// TODO hartez 2018/06/01 14:21:24 Add a method for updating the layout	
	public class ItemsViewController : UICollectionViewController
	{
		bool _initialConstraintsSet;
		bool _wasEmpty;
		bool _currentBackgroundIsEmptyView;

		UIView _backgroundUIView;
		UIView _emptyUIView;
		VisualElement _emptyViewFormsElement;

		protected UICollectionViewDelegator Delegator { get; set; }
		protected IItemsViewSource ItemsSource { get; set; }

		public ItemsView ItemsView { get; }

		protected ItemsViewLayout ItemsViewLayout { get; }

		public ItemsViewController(ItemsView itemsView, ItemsViewLayout layout) : base(layout)
		{
			ItemsView = itemsView;
			ItemsSource = ItemsSourceFactory.Create(_itemsView.ItemsSource, CollectionView);

			UpdateLayout(layout);
		}

		public void UpdateLayout(ItemsViewLayout layout)
		{
			ItemsViewLayout = layout;
			ItemsViewLayout.GetPrototype = GetPrototype;

			// If we're updating from a previous layout, we should keep any settings for the SelectableItemsViewController around
			var selectableItemsViewController = Delegator?.SelectableItemsViewController;
			Delegator = new UICollectionViewDelegator(_layout, this);

			CollectionView.Delegate = Delegator;

			if (CollectionView.CollectionViewLayout != ItemsViewLayout)
			{
				// We're updating from a previous layout

				// Make sure the new layout is sized properly
				ItemsViewLayout.ConstrainTo(CollectionView.Bounds.Size);
				
				
				// Reload the data so the currently visible cells get laid out according to the new layout
				CollectionView.ReloadData();
			}
		}

		public override UICollectionViewCell GetCell(UICollectionView collectionView, NSIndexPath indexPath)
		{
			var cell = collectionView.DequeueReusableCell(DetermineCellReuseId(), indexPath) as UICollectionViewCell;

			switch (cell)
			{
				case DefaultCell defaultCell:
					UpdateDefaultCell(defaultCell, indexPath);
					break;
				case TemplatedCell templatedCell:
					UpdateTemplatedCell(templatedCell, indexPath);
					break;
			}

			return cell;
		}

		public override nint GetItemsCount(UICollectionView collectionView, nint section)
		{
			var count = ItemsSource.ItemCountInGroup(section);

			CheckForEmptySource();

			return count;
		}

		private void CheckForEmptySource()
		{
			var totalCount = ItemsSource.ItemCount;

			if (_wasEmpty && totalCount > 0)
			{
				// We've moved from no items to having at least one item; it's likely that the layout needs to update
				// its cell size/estimate
				ItemsViewLayout?.SetNeedCellSizeUpdate();
			}

			_wasEmpty = totalCount == 0;

			UpdateEmptyViewVisibility(_wasEmpty);
		}

		public override void ViewDidLoad()
		{
			base.ViewDidLoad();
			AutomaticallyAdjustsScrollViewInsets = false;
			RegisterCells();
		}

		public override void ViewWillLayoutSubviews()
		{
			base.ViewWillLayoutSubviews();

			// We can't set this constraint up on ViewDidLoad, because Forms does other stuff that resizes the view
			// and we end up with massive layout errors. And View[Will/Did]Appear do not fire for this controller
			// reliably. So until one of those options is cleared up, we set this flag so that the initial constraints
			// are set up the first time this method is called.
			if (!_initialConstraintsSet)
			{
				ItemsViewLayout.ConstrainTo(CollectionView.Bounds.Size);
				_initialConstraintsSet = true;
			}
		}

		protected virtual IItemsViewSource CreateItemsViewSource()
		{
			var x = ItemsView.ItemsSource;


			return ItemsSourceFactory.Create(x, CollectionView);
		}

		public virtual void UpdateItemsSource()
		{
			ItemsSource = CreateItemsViewSource();
			CollectionView.ReloadData();
			CollectionView.CollectionViewLayout.InvalidateLayout();
		}

		protected virtual void UpdateDefaultCell(DefaultCell cell, NSIndexPath indexPath)
		{
			cell.Label.Text = ItemsSource[indexPath].ToString();

			if (cell is ItemsViewCell constrainedCell)
			{
				ItemsViewLayout.PrepareCellForLayout(constrainedCell);
			}
		}

		protected virtual void UpdateTemplatedCell(TemplatedCell cell, NSIndexPath indexPath)
		{
			ApplyTemplateAndDataContext(cell, indexPath);

			if (cell is ItemsViewCell constrainedCell)
			{
				ItemsViewLayout.PrepareCellForLayout(constrainedCell);
			}
		}

		public virtual NSIndexPath GetIndexForItem(object item)
		{
			return ItemsSource.GetIndexForItem(item);
		}

		protected object GetItemAtIndex(NSIndexPath index)
		{
			return ItemsSource[index];
		}

		void ApplyTemplateAndDataContext(TemplatedCell cell, NSIndexPath indexPath)
		{
			// We need to create a renderer, which means we need a template
			var view = _itemsView.ItemTemplate.CreateContent() as View;
			_itemsView.AddLogicalChild(view);
			var renderer = CreateRenderer(view);
			BindableObject.SetInheritedBindingContext(view, _itemsSource[indexPath.Row]);
			cell.SetRenderer(renderer);
		}

		internal void RemoveLogicalChild(UICollectionViewCell cell)
		{
			if (cell is TemplatedCell templatedCell)
			{
				var oldView = templatedCell.VisualElementRenderer?.Element;
				if (oldView != null)
				{
					_itemsView.RemoveLogicalChild(oldView);
				}
			}
		}

		protected IVisualElementRenderer CreateRenderer(View view)
		{
			if (view == null)
			{
				throw new ArgumentNullException(nameof(view));
			}

			var renderer = Platform.CreateRenderer(view);
			Platform.SetRenderer(view, renderer);

			return renderer;
		}

		string DetermineCellReuseId()
		{
			if (ItemsView.ItemTemplate != null)
			{
				return ItemsViewLayout.ScrollDirection == UICollectionViewScrollDirection.Horizontal
					? HorizontalTemplatedCell.ReuseId
					: VerticalTemplatedCell.ReuseId;
			}

			return ItemsViewLayout.ScrollDirection == UICollectionViewScrollDirection.Horizontal
				? HorizontalDefaultCell.ReuseId
				: VerticalDefaultCell.ReuseId;
		}

		UICollectionViewCell GetPrototype()
		{
			// TODO hartez see TOOD below

			if (ItemsSource.ItemCount == 0)
			{
				return null;
			}

			// TODO hartez assuming this works, we'll need to evaluate using this nsindexpath (what about groups?)
			var indexPath = NSIndexPath.Create(0, 0);
			return GetCell(CollectionView, indexPath);
		}

		protected virtual void RegisterCells()
		{
			CollectionView.RegisterClassForCell(typeof(HorizontalDefaultCell), HorizontalDefaultCell.ReuseId);
			CollectionView.RegisterClassForCell(typeof(VerticalDefaultCell), VerticalDefaultCell.ReuseId);
			CollectionView.RegisterClassForCell(typeof(HorizontalTemplatedCell),
				HorizontalTemplatedCell.ReuseId);
			CollectionView.RegisterClassForCell(typeof(VerticalTemplatedCell), VerticalTemplatedCell.ReuseId);
		}

		internal void UpdateEmptyView()
		{
			// Is EmptyView set on the ItemsView?
			var emptyView = ItemsView?.EmptyView;

			if (emptyView == null)
			{
				// Clear the cached Forms and native views
				_emptyUIView = null;
				_emptyViewFormsElement = null;
			}
			else
			{
				// Create the native renderer for the EmptyView, and keep the actual Forms element (if any)
				// around for updating the layout later
				var (NativeView, FormsElement) = RealizeEmptyView(emptyView, ItemsView.EmptyViewTemplate);
				_emptyUIView = NativeView;
				_emptyViewFormsElement = FormsElement;
			}

			// If the empty view is being displayed, we might need to update it
			UpdateEmptyViewVisibility(_itemsSource?.Count == 0);
		}

		void UpdateEmptyViewVisibility(bool isEmpty)
		{
			if (isEmpty && _emptyUIView != null)
			{
				if (!_currentBackgroundIsEmptyView)
				{
					// Cache any existing background view so we can restore it later
					_backgroundUIView = CollectionView.BackgroundView;
				}

				// Replace any current background with the EmptyView. This will also set the native empty view's frame
				// to match the UICollectionView's frame
				CollectionView.BackgroundView = _emptyUIView;
				_currentBackgroundIsEmptyView = true;

				if (_emptyViewFormsElement != null)
				{
					// Now that the native empty view's frame is sized to the UICollectionView, we need to handle
					// the Forms layout for its content
					_emptyViewFormsElement.Layout(_emptyUIView.Frame.ToRectangle());
				}
			}
			else
			{
				// Is the empty view currently in the background? Swap back to the default.
				if (_currentBackgroundIsEmptyView)
				{
					CollectionView.BackgroundView = _backgroundUIView;
				}

				_currentBackgroundIsEmptyView = false;
			}
		}

		public (UIView NativeView, VisualElement FormsElement) RealizeEmptyView(object emptyView, DataTemplate emptyViewTemplate)
		{
			if (emptyViewTemplate != null)
			{
				// We have a template; turn it into a Forms view 
				var templateElement = emptyViewTemplate.CreateContent() as View;
				var renderer = CreateRenderer(templateElement);

				// and set the EmptyView as its BindingContext
				BindableObject.SetInheritedBindingContext(renderer.Element, emptyView);

				return (renderer.NativeView, renderer.Element);
			}

			if (emptyView is View formsView)
			{
				// No template, and the EmptyView is a Forms view; use that
				var renderer = CreateRenderer(formsView);

				return (renderer.NativeView, renderer.Element);
			}

			// No template, EmptyView is not a Forms View, so just display EmptyView.ToString
			var label = new UILabel { Text = emptyView.ToString() };
			return (label, null);
		}
	}
}