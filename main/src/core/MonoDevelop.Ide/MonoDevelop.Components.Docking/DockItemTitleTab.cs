//
// DockItemTitleTab.cs
//
// Author:
//       Lluis Sanchez Gual <lluis@xamarin.com>
//
// Copyright (c) 2012 Xamarin Inc. (http://xamarin.com)
// Copyright (C) 2007 Novell, Inc (http://www.novell.com)
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using Gtk; 

using System;
using MonoDevelop.Ide.Gui;
using System.Linq;
using MonoDevelop.Core;
using MonoDevelop.Ide;
using MonoDevelop.Components;
using MonoDevelop.Ide.Fonts;

namespace MonoDevelop.Components.Docking
{
	
	class DockItemTitleTab: Gtk.EventBox
	{
		static Xwt.Drawing.Image dockTabActiveBackImage = Xwt.Drawing.Image.FromResource ("padbar-active.9.png");
		static Xwt.Drawing.Image dockTabBackImage = Xwt.Drawing.Image.FromResource ("padbar-inactive.9.png");

		bool active;
		Gtk.Widget page;
		ExtendedLabel labelWidget;
		int labelWidth;
		DockVisualStyle visualStyle;
		ImageView tabIcon;
		DockFrame frame;
		string label;
		ImageButton btnDock;
		ImageButton btnClose;
		DockItem item;
		bool allowPlaceholderDocking;
		bool mouseOver;

		IDisposable subscribedLeaveEvent;

		static Gdk.Cursor fleurCursor = new Gdk.Cursor (Gdk.CursorType.Fleur);

		static Xwt.Drawing.Image pixClose;
		static Xwt.Drawing.Image pixAutoHide;
		static Xwt.Drawing.Image pixDock;

		static double PixelScale = GtkWorkarounds.GetPixelScale ();

		static readonly Xwt.WidgetSpacing TabPadding;
		static readonly Xwt.WidgetSpacing TabActivePadding;

		static DockItemTitleTab ()
		{
			pixClose = Xwt.Drawing.Image.FromResource ("pad-close-9.png");
			pixAutoHide = Xwt.Drawing.Image.FromResource ("pad-minimize-9.png");
			pixDock = Xwt.Drawing.Image.FromResource ("pad-dock-9.png");

			Xwt.Drawing.NinePatchImage tabBackImage9;
			if (dockTabBackImage is Xwt.Drawing.ThemedImage) {
				var img = ((Xwt.Drawing.ThemedImage)dockTabBackImage).GetImage (Xwt.Drawing.Context.GlobalStyles);
				tabBackImage9 = img as Xwt.Drawing.NinePatchImage;
			} else
				tabBackImage9 = dockTabBackImage as Xwt.Drawing.NinePatchImage;
			TabPadding = tabBackImage9.Padding;


			Xwt.Drawing.NinePatchImage tabActiveBackImage9;
			if (dockTabActiveBackImage is Xwt.Drawing.ThemedImage) {
				var img = ((Xwt.Drawing.ThemedImage)dockTabActiveBackImage).GetImage (Xwt.Drawing.Context.GlobalStyles);
				tabActiveBackImage9 = img as Xwt.Drawing.NinePatchImage;
			} else
				tabActiveBackImage9 = dockTabActiveBackImage as Xwt.Drawing.NinePatchImage;
			TabActivePadding = tabActiveBackImage9.Padding;
		}
		
		public DockItemTitleTab (DockItem item, DockFrame frame)
		{
			this.item = item;
			this.frame = frame;
			this.VisibleWindow = false;
			UpdateVisualStyle ();
			NoShowAll = true;


			Events |= Gdk.EventMask.EnterNotifyMask | Gdk.EventMask.LeaveNotifyMask | Gdk.EventMask.ButtonPressMask | Gdk.EventMask.ButtonReleaseMask | Gdk.EventMask.PointerMotionMask;
			KeyPressEvent += HeaderKeyPress;
			KeyReleaseEvent += HeaderKeyRelease;

			subscribedLeaveEvent = this.SubscribeLeaveEvent (OnLeave);
		}

		public DockVisualStyle VisualStyle {
			get { return visualStyle; }
			set {
				visualStyle = value;
				UpdateVisualStyle ();
				QueueDraw ();
			}
		}

		protected override void OnDestroyed ()
		{
			subscribedLeaveEvent.Dispose ();
			base.OnDestroyed ();
		}

		void UpdateVisualStyle ()
		{
			double inactiveIconAlpha;

			if (IdeApp.Preferences == null || IdeApp.Preferences.UserInterfaceSkin == Skin.Light)
				inactiveIconAlpha = 0.8;
			else
				inactiveIconAlpha = 0.6;

			if (labelWidget != null && label != null) {
				if (visualStyle.UppercaseTitles.Value)
					labelWidget.Text = label.ToUpper ();
				else
					labelWidget.Text = label;
				labelWidget.UseMarkup = true;
				if (visualStyle.ExpandedTabs.Value)
					labelWidget.Xalign = 0.5f;

				if (!(Parent is TabStrip.TabStripBox))
					labelWidget.Xalign = 0;
			}

			if (tabIcon != null) {
				tabIcon.Image = tabIcon.Image.WithAlpha (active ? 1.0 : inactiveIconAlpha);
				tabIcon.Visible = visualStyle.ShowPadTitleIcon.Value;
			}
			if (IsRealized && labelWidget != null) {
				var font = FontService.SansFont.CopyModified (Styles.FontScale11, Pango.Weight.Bold);
				labelWidget.ModifyFont (font);
				labelWidget.ModifyText (StateType.Normal, (active ? visualStyle.PadTitleLabelColor.Value : visualStyle.InactivePadTitleLabelColor.Value).ToGdkColor ());
			}

			var r = WidthRequest;
			WidthRequest = -1;
			labelWidth = SizeRequest ().Width + 1;
			WidthRequest = r;

			if (visualStyle != null)
				HeightRequest = visualStyle.PadTitleHeight != null ? (int)(visualStyle.PadTitleHeight.Value * PixelScale) : -1;
		}

		public void SetLabel (Gtk.Widget page, Xwt.Drawing.Image icon, string label)
		{
			this.label = label;
			this.page = page;
			if (Child != null) {
				Gtk.Widget oc = Child;
				Remove (oc);
				oc.Destroy ();
			}
			
			Gtk.HBox box = new HBox ();
			box.Spacing = -2;
			
			if (icon == null)
				icon = ImageService.GetIcon ("md-empty");

			tabIcon = new ImageView (icon);
			tabIcon.Show ();
			box.PackStart (tabIcon, false, false, 3);

			if (!string.IsNullOrEmpty (label)) {
				labelWidget = new ExtendedLabel (label);
				labelWidget.UseMarkup = true;
				labelWidget.Yalign = 0.85f;
				var alignLabel = new Alignment (0.0f, 1.0f, 1, 1);
				alignLabel.BottomPadding = 0;
				alignLabel.RightPadding = 15;
				alignLabel.Add (labelWidget);
				box.PackStart (alignLabel, true, true, 0);
			} else {
				labelWidget = null;
			}

			btnDock = new ImageButton ();
			btnDock.Image = pixAutoHide;
			btnDock.TooltipText = GettextCatalog.GetString ("Auto Hide");
			btnDock.CanFocus = false;
//			btnDock.WidthRequest = btnDock.HeightRequest = 17;
			btnDock.Clicked += OnClickDock;
			btnDock.ButtonPressEvent += (o, args) => args.RetVal = true;
			btnDock.WidthRequest = btnDock.SizeRequest ().Width;

			btnClose = new ImageButton ();
			btnClose.Image = pixClose;
			btnClose.TooltipText = GettextCatalog.GetString ("Close");
			btnClose.CanFocus = false;
//			btnClose.WidthRequest = btnClose.HeightRequest = 17;
			btnClose.WidthRequest = btnDock.SizeRequest ().Width;
			btnClose.Clicked += delegate {
				item.Visible = false;
			};
			btnClose.ButtonPressEvent += (o, args) => args.RetVal = true;

			Gtk.Alignment al = new Alignment (0, 0.5f, 1, 1);
			HBox btnBox = new HBox (false, 0);
			btnBox.PackStart (btnDock, false, false, 3);
			btnBox.PackStart (btnClose, false, false, 1);
			al.Add (btnBox);
			box.PackEnd (al, false, false, 3);

			Add (box);
			
			// Get the required size before setting the ellipsize property, since ellipsized labels
			// have a width request of 0
			box.ShowAll ();
			Show ();

			UpdateBehavior ();
			UpdateVisualStyle ();
		}
		
		void OnClickDock (object s, EventArgs a)
		{
			if (item.Status == DockItemStatus.AutoHide || item.Status == DockItemStatus.Floating)
				item.Status = DockItemStatus.Dockable;
			else
				item.Status = DockItemStatus.AutoHide;
		}

		public int LabelWidth {
			get { return labelWidth; }
		}
		
		public bool Active {
			get {
				return active;
			}
			set {
				if (active != value) {
					active = value;
					UpdateVisualStyle ();
					QueueResize ();
					QueueDraw ();
					UpdateBehavior ();
				}
			}
		}

		public Widget Page {
			get {
				return page;
			}
		}
		
		public void UpdateBehavior ()
		{
			if (btnClose == null)
				return;

			btnClose.Visible = (item.Behavior & DockItemBehavior.CantClose) == 0;
			btnDock.Visible = (item.Behavior & DockItemBehavior.CantAutoHide) == 0;
			
			if (active || mouseOver) {
				if (btnClose.Image == null)
					btnClose.Image = pixClose;
				if (item.Status == DockItemStatus.AutoHide || item.Status == DockItemStatus.Floating) {
					btnDock.Image = pixDock;
					btnDock.TooltipText = GettextCatalog.GetString ("Dock");
				} else {
					btnDock.Image = pixAutoHide;
					btnDock.TooltipText = GettextCatalog.GetString ("Auto Hide");
				}
			} else {
				btnDock.Image = null;
				btnClose.Image = null;
			}
		}

		bool tabPressed, tabActivated;
		double pressX, pressY;

		protected override bool OnButtonPressEvent (Gdk.EventButton evnt)
		{
			if (evnt.TriggersContextMenu ()) {
				item.ShowDockPopupMenu (evnt.Time);
				return false;
			} else if (evnt.Button == 1) {
				if (evnt.Type == Gdk.EventType.ButtonPress) {
					tabPressed = true;
					pressX = evnt.X;
					pressY = evnt.Y;
				} else if (evnt.Type == Gdk.EventType.TwoButtonPress) {
					tabActivated = true;
				}
			}
			return base.OnButtonPressEvent (evnt);
		}

		protected override bool OnButtonReleaseEvent (Gdk.EventButton evnt)
		{
			if (tabActivated) {
				tabActivated = false;
				if (!item.Behavior.HasFlag (DockItemBehavior.CantAutoHide)) {
					if (item.Status == DockItemStatus.AutoHide)
						item.Status = DockItemStatus.Dockable;
					else
						item.Status = DockItemStatus.AutoHide;
				}
			}
			else if (!evnt.TriggersContextMenu () && evnt.Button == 1) {
				frame.DockInPlaceholder (item);
				frame.HidePlaceholder ();
				if (GdkWindow != null)
					GdkWindow.Cursor = null;
				frame.Toplevel.KeyPressEvent -= HeaderKeyPress;
				frame.Toplevel.KeyReleaseEvent -= HeaderKeyRelease;
			}
			tabPressed = false;
			return base.OnButtonReleaseEvent (evnt);
		}

		protected override bool OnMotionNotifyEvent (Gdk.EventMotion evnt)
		{
			if (tabPressed && !item.Behavior.HasFlag (DockItemBehavior.NoGrip) && Math.Abs (evnt.X - pressX) > 3 && Math.Abs (evnt.Y - pressY) > 3) {
				frame.ShowPlaceholder (item);
				GdkWindow.Cursor = fleurCursor;
				frame.Toplevel.KeyPressEvent += HeaderKeyPress;
				frame.Toplevel.KeyReleaseEvent += HeaderKeyRelease;
				allowPlaceholderDocking = true;
				tabPressed = false;
			}
			frame.UpdatePlaceholder (item, Allocation.Size, allowPlaceholderDocking);
			return base.OnMotionNotifyEvent (evnt);
		}

		protected override bool OnEnterNotifyEvent (Gdk.EventCrossing evnt)
		{
			mouseOver = true;
			UpdateBehavior ();
			return base.OnEnterNotifyEvent (evnt);
		}

		void OnLeave ()
		{
			mouseOver = false;
			UpdateBehavior ();
		}

		[GLib.ConnectBeforeAttribute]
		void HeaderKeyPress (object ob, Gtk.KeyPressEventArgs a)
		{
			if (a.Event.Key == Gdk.Key.Control_L || a.Event.Key == Gdk.Key.Control_R) {
				allowPlaceholderDocking = false;
				frame.UpdatePlaceholder (item, Allocation.Size, false);
			}
			if (a.Event.Key == Gdk.Key.Escape) {
				frame.HidePlaceholder ();
				frame.Toplevel.KeyPressEvent -= HeaderKeyPress;
				frame.Toplevel.KeyReleaseEvent -= HeaderKeyRelease;
				Gdk.Pointer.Ungrab (0);
			}
		}
		
		[GLib.ConnectBeforeAttribute]
		void HeaderKeyRelease (object ob, Gtk.KeyReleaseEventArgs a)
		{
			if (a.Event.Key == Gdk.Key.Control_L || a.Event.Key == Gdk.Key.Control_R) {
				allowPlaceholderDocking = true;
				frame.UpdatePlaceholder (item, Allocation.Size, true);
			}
		}

		protected override void OnRealized ()
		{
			base.OnRealized ();
			UpdateVisualStyle ();
		}
		
		protected override void OnSizeRequested (ref Gtk.Requisition req)
		{
			if (Child != null) {
				req = Child.SizeRequest ();
				req.Width += (int)(TabPadding.Left + TabPadding.Right);
				if (active)
					req.Height += (int)(TabActivePadding.Top + TabActivePadding.Bottom);
				else
					req.Height += (int)(TabPadding.Top + TabPadding.Bottom);
			}
		}
					
		protected override void OnSizeAllocated (Gdk.Rectangle rect)
		{
			base.OnSizeAllocated (rect);

			int leftPadding = (int)TabPadding.Left;
			int rightPadding = (int)TabPadding.Right;
			if (rect.Width < labelWidth) {
				int red = (labelWidth - rect.Width) / 2;
				leftPadding -= red;
				rightPadding -= red;
				if (leftPadding < 2) leftPadding = 2;
				if (rightPadding < 2) rightPadding = 2;
			}
			
			rect.X += leftPadding;
			rect.Width -= leftPadding + rightPadding;
			if (rect.Width < 1) {
				rect.Width = 1;
			}

			if (Child != null) {
				var bottomPadding = active ? (int)TabActivePadding.Bottom : (int)TabPadding.Bottom;
				var topPadding = active ? (int)TabActivePadding.Top : (int)TabPadding.Top;
				int centerY = topPadding + ((rect.Height - bottomPadding - topPadding) / 2);
				var height = Child.SizeRequest ().Height;
				rect.Y += centerY - (height / 2);
				rect.Height = height;
				Child.SizeAllocate (rect);
			}
		}

		protected override bool OnExposeEvent (Gdk.EventExpose evnt)
		{
			if (VisualStyle.TabStyle == DockTabStyle.Normal)
				DrawAsBrowser (evnt);
			else
				DrawNormal (evnt);
			return base.OnExposeEvent (evnt);
		}

		void DrawAsBrowser (Gdk.EventExpose evnt)
		{
			bool first = true;
			bool last = true;
			TabStrip tabStrip = null;
			if (Parent is TabStrip.TabStripBox) {
				var tsb = (TabStrip.TabStripBox) Parent;
				var cts = tsb.Children;
				first = cts[0] == this;
				last = cts[cts.Length - 1] == this;
				tabStrip = tsb.TabStrip;
			}

			using (var ctx = Gdk.CairoHelper.Create (GdkWindow)) {
				if (first && last) {
					ctx.Rectangle (Allocation.X, Allocation.Y, Allocation.Width, Allocation.Height);
					ctx.SetSourceColor (VisualStyle.PadBackgroundColor.Value.ToCairoColor ());
					ctx.Fill ();
				} else {
					var image = Active ? dockTabActiveBackImage : dockTabBackImage;
					image = image.WithSize (Allocation.Width, Allocation.Height);

					ctx.DrawImage (this, image, Allocation.X, Allocation.Y);
				}
			}
		}

		void DrawNormal (Gdk.EventExpose evnt)
		{
			using (var ctx = Gdk.CairoHelper.Create (GdkWindow)) {
				var x = Allocation.X;
				var y = Allocation.Y;

				ctx.Rectangle (x, y + 1, Allocation.Width, Allocation.Height - 1);
				ctx.SetSourceColor (Styles.DockBarBackground.ToCairoColor ());
				ctx.Fill ();

				/*
				if (active) {
					double offset = Allocation.Height * 0.25;
					var rect = new Cairo.Rectangle (x - Allocation.Height + offset, y, Allocation.Height, Allocation.Height);
					var cg = new Cairo.RadialGradient (rect.X + rect.Width / 2, rect.Y + rect.Height / 2, 0, rect.X, rect.Y + rect.Height / 2, rect.Height / 2);
					cg.AddColorStop (0, Styles.DockTabBarShadowGradientStart);
					cg.AddColorStop (1, Styles.DockTabBarShadowGradientEnd);
					ctx.Pattern = cg;
					ctx.Rectangle (rect);
					ctx.Fill ();

					rect = new Cairo.Rectangle (x + Allocation.Width - offset, y, Allocation.Height, Allocation.Height);
					cg = new Cairo.RadialGradient (rect.X + rect.Width / 2, rect.Y + rect.Height / 2, 0, rect.X, rect.Y + rect.Height / 2, rect.Height / 2);
					cg.AddColorStop (0, Styles.DockTabBarShadowGradientStart);
					cg.AddColorStop (1, Styles.DockTabBarShadowGradientEnd);
					ctx.Pattern = cg;
					ctx.Rectangle (rect);
					ctx.Fill ();
				}
				*/
			}
		}
	}
}
