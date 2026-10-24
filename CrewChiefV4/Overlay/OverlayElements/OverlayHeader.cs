using GameOverlay.Drawing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GameOverlay.Windows;
using CrewChiefV4.Events;
using GameOverlay.PInvoke;
using System.Windows.Forms;

namespace CrewChiefV4.Overlay
{
    public class OverlayHeader : OverlayElement
    {
        private Image icon;
        private bool modifierPressed = false;
        private int cursorOffsetInRectX = 0;
        private int cursorOffsetInRectY = 0;
        public bool windowActive = false;
        private string elementTitle;
        private readonly GraphicsWindow overlayWindow;
        public event EventHandler<OverlayElementClicked> OnCheckBoxEnableInputClicked;
        public event EventHandler<OverlayElementClicked> OnButtonClose;
        public event EventHandler<OverlayElementClicked> OnButtonSaveWindowPosition;
        public static byte[] ImageToByte(System.Drawing.Image img)
        {
            byte[] byteArray = new byte[0];
            using (MemoryStream stream = new MemoryStream())
            {
                img.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                stream.Close();

                byteArray = stream.ToArray();
            }
            return byteArray;
        }

        public OverlayHeader(Graphics gfx, string elementTitle, Font font, System.Windows.Rect rectangle, ColorScheme colorScheme, GraphicsWindow overlayWindow, 
            EventHandler<OverlayElementClicked> OnCheckBoxEnableInputClicked, EventHandler<OverlayElementClicked> OnButtonClose, 
            EventHandler<OverlayElementClicked> OnButtonSaveWindowPosition = null, bool initialEnabled = true) : 
            base(gfx, elementTitle, font, rectangle, colorScheme, initialEnabled)
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainWindow));
            System.Drawing.Icon iconImage = (System.Drawing.Icon)(resources.GetObject("$this.Icon"));
            iconImage = new System.Drawing.Icon(iconImage, new System.Drawing.Size(16, 16));
            this.icon = new Image(gfx, ImageToByte(iconImage.ToBitmap()));                                         
            base.parent = this;
            this.overlayWindow = overlayWindow;
            this.OnCheckBoxEnableInputClicked += OnCheckBoxEnableInputClicked;            
            this.OnButtonClose += OnButtonClose;
            this.OnButtonSaveWindowPosition += OnButtonSaveWindowPosition;
            this.elementTitle = elementTitle;
        }
        public override void initialize()
        {
            //base.initialize();
            foreach (var child in children)
            {
                if (child.title == "Enable input")
                {
                    OnCheckBoxEnableInputClicked?.Invoke(this, new OverlayElementClicked(child.gfx, ((ElementCheckBox)child).enabled));

                    child.OnEnterKeyDown += OnCheckBoxEnableInputClicked;
                    child.OnElementLMButtonClicked += OnCheckBoxEnableInputClicked;
                }
                if (child.title == "ButtonClose")
                {
                    child.OnEnterKeyDown += OnButtonClose;
                    child.OnElementLMButtonClicked += OnButtonClose;
                }
            }
        }
        public override void drawElement()
        {
            if (!this.elementEnabled)
                return;
           
            gfx.DrawBox2D(base.secondaryBrush, base.primaryBrush, new Rectangle(base.rectangle), 1);
            gfx.DrawText(base.font, 12, base.secondaryBrush, 20, 3, elementTitle);           
            gfx.DrawImage(this.icon, 2, 2, 1);
            foreach(var child in children)
            {
                child.drawElement();
            }
            return;
        }
        public override void updateInputs(int overlayWindowX, int overlayWindowY, bool inputEnabled)
        {
            if (!this.elementEnabled)
                return;
            isMouseOver(overlayWindowX, overlayWindowY, inputEnabled);
            foreach (var child in children)
            {
                IsMouseDownInElement(); 
                if(child.mousePressed)
                {
                    IsMouseUpInElement();
                    break;
                }                           
            }             
            return;
        }
        private bool IsMouseDownInElement()
        {
            System.Drawing.Point cursor = new System.Drawing.Point(System.Windows.Forms.Cursor.Position.X, System.Windows.Forms.Cursor.Position.Y);
            if (!this.elementEnabled)
                return false;
            foreach(var child in children)
            {
                if (child.mouseOver)
                {
                    if ((System.Windows.Forms.Control.MouseButtons == System.Windows.Forms.MouseButtons.Left))
                    {
                        child.mousePressed = true;
                    }
                }
                else
                {
                    child.mousePressed = false;
                }
            }
            return false;
        }
        private void isMouseOver(int overlayWindowX, int overlayWindowY, bool inputEnabled)
        {
            System.Drawing.Point cursor = new System.Drawing.Point(System.Windows.Forms.Cursor.Position.X, System.Windows.Forms.Cursor.Position.Y);
            foreach (var child in children)
            {
                System.Windows.Rect rect = child.rectangle;
                rect.X += overlayWindowX;
                rect.Y += overlayWindowY;
                
                child.mouseOver = rect.Contains(cursor.X, cursor.Y);
                if (child.mouseOver && !windowActive && !inputEnabled)
                {
                    overlayWindow.ActivateWindow();
                    windowActive = true;
                }
                else if(windowActive && !inputEnabled)
                {
                    overlayWindow.DeActivateWindow();
                    windowActive = false;
                }
            }      
            return;
        }
        private void IsMouseUpInElement()
        {
            if (!this.elementEnabled)
                return;
            foreach (var child in children)
            {
                if (child.mouseOver && child.mousePressed && (System.Windows.Forms.Control.MouseButtons != System.Windows.Forms.MouseButtons.Left))
                {
                    if (child.title == "ButtonClose")
                    {
                        OnButtonClose?.Invoke(this, new OverlayElementClicked(child.gfx, false));
                        windowActive = false;
                        //OnButtonClosed();
                    }
                    if(child.title == "Enable input")
                    {
                        ((ElementCheckBox)child).enabled = !((ElementCheckBox)child).enabled;
                        OnCheckBoxEnableInputClicked?.Invoke(this, new OverlayElementClicked(child.gfx, ((ElementCheckBox)child).enabled));
                    }
                    if (child.title == "Save window position")
                    {
                        OnButtonSaveWindowPosition?.Invoke(this, new OverlayElementClicked(child.gfx, false));
                    }
                    child.mousePressed = false;
                }
            }
        }
        public override bool OnWindowMessage(WindowMessage message, IntPtr wParam, IntPtr lParam)
        {
            if (!this.elementEnabled)
                return false;
            foreach (var child in children)
            {
                if(child.OnWindowMessage(message, wParam, lParam))
                    return false;
            }

            if (message == WindowMessage.Mousemove)
            {
                mousePosition.X = WindowNativeMethods.GET_X_LPARAM(lParam);
                mousePosition.Y = WindowNativeMethods.GET_Y_LPARAM(lParam);
                if(rectangle.Contains(mousePosition))
                {
                    mouseOver = true;
                }
                else
                {
                    mouseOver = false;
                }
            }
            if (mouseOver)
            {
                System.Drawing.Point cursor = new System.Drawing.Point(System.Windows.Forms.Cursor.Position.X, System.Windows.Forms.Cursor.Position.Y);
                if (message == WindowMessage.Lbuttondown)
                {                   
                    if (!modifierPressed)
                    {
                        cursorOffsetInRectX = (int)cursor.X - overlayWindow.X;
                        cursorOffsetInRectY = (int)cursor.Y - overlayWindow.Y;
                        modifierPressed = true;
                    }                                   
                }
                if(message == WindowMessage.Mousemove && modifierPressed)
                {
                    overlayWindow.Move((int)cursor.X - cursorOffsetInRectX, (int)cursor.Y - cursorOffsetInRectY);
                }
            }
            if (message == WindowMessage.Lbuttonup)
            {
                modifierPressed = false;
            }
            return false;
        }
    }
}
