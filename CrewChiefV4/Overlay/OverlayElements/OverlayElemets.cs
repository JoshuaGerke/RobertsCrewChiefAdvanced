using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using GameOverlay.Drawing;
using GameOverlay.Windows;
using GameOverlay.PInvoke;
using System.Windows.Forms;

namespace CrewChiefV4.Overlay
{
    #region event args
    public class OverlayElementClicked : EventArgs
    {

        private OverlayElementClicked()
        {
        }
        public bool enabled { get; private set; }
        public string costumTextId { get; private set; }
        public Graphics graphics { get; private set; }
        public OverlayElementClicked(Graphics graphics = null, bool enabled = false, string costumTextId = null)
        {
            this.enabled = enabled;
            this.costumTextId = costumTextId;
            this.graphics = graphics;
        }        
    }
    public class OverlayElementMouseWheel : EventArgs
    {

        private OverlayElementMouseWheel()
        {
        }

        public Graphics graphics { get; private set; }
        public int UpDown { get; private set; }
        
        public OverlayElementMouseWheel(Graphics graphics, int UpDown)
        {
            this.graphics = graphics;
            this.UpDown = UpDown;
        }
    }

    public class OverlayElementDrawUpdate: EventArgs
    {

        private OverlayElementDrawUpdate()
        {
        }
        public Graphics graphics { get; private set; }
        public Rectangle rect { get; private set; }
        public OverlayElementDrawUpdate(Graphics graphics, Rectangle rect)
        {
            this.graphics = graphics;
            this.rect = rect;
        }
    }
    public class OverlayElementKeyDown : EventArgs
    {

        public OverlayElementKeyDown(Graphics gfx, Keys key)
        {
            this.graphics = graphics;
            this.key = key;
        }
        public Graphics graphics { get; private set; }
        public Keys key { get; private set; }
    }
    #endregion

    public class OverlayElement
    {
        public string title;
        public Rect rectangle;
        public Rect fontRectangle;
        public SolidBrush primaryBrush;
        public SolidBrush secondaryBrush;
        public SolidBrush transparentBrush;
        public bool mousePressed;
        public bool mouseOver;
        public Font font;
        public Graphics gfx;
        public OverlayElement parent = null;
        public List<OverlayElement> children { get; private set; }
        public ColorScheme colorScheme;
        public bool elementEnabled;
        public System.Windows.Point mousePosition = new System.Windows.Point(0, 0);
        public bool includeFontRectInMouseOver = false;
        public int tabIndex = -1;
        public bool selected = false;
        public EventHandler<OverlayElementClicked> OnElementLMButtonClicked;
        public EventHandler<OverlayElementClicked> OnElementMMButtonClicked;
        public EventHandler<OverlayElementMouseWheel> OnElementMWheel;
        public EventHandler<OverlayElementDrawUpdate> OnElementDraw;
        public EventHandler<OverlayElementKeyDown> OnKeyDown;
        public EventHandler<OverlayElementClicked> OnEnterKeyDown;

        public OverlayElement(Graphics gfx, string elementTitle, Font font, Rect rectangle, ColorScheme colorScheme, bool initialState = true)
        {
            this.title = elementTitle;
            this.rectangle = rectangle;
            this.colorScheme = colorScheme;
            this.primaryBrush = gfx.CreateSolidBrush(colorScheme.backgroundColor);
            this.secondaryBrush = gfx.CreateSolidBrush(colorScheme.fontColor);
            this.transparentBrush = gfx.CreateSolidBrush(Color.Transparent);
            this.font = font;
            this.gfx = gfx;
            this.children = new List<OverlayElement>();
            this.elementEnabled = initialState;
            System.Drawing.SizeF fontSize = font.MeasureString(elementTitle);
            this.fontRectangle = new Rect(rectangle.Right, rectangle.Y, fontSize.Width + 4, fontSize.Height);
        }
        public virtual void initialize()
        {
            foreach (var element in children)
            {
                element.initialize();
            }
        }
        public Rect getAbsolutePosition()
        {
            Rect rect = rectangle;
            var lastParent = parent;
            while (lastParent != null)
            {
                rect.Y += lastParent.rectangle.Y;
                rect.X += lastParent.rectangle.X;
                if (lastParent != lastParent.parent)
                {
                    lastParent = lastParent.parent;
                }
                else
                {
                    break;
                }
            }
            return rect;
        }
        public Rect getTextAbsolutePosition()
        {
            Rect rect = fontRectangle;
            var lastParent = parent;
            while (lastParent != null)
            {
                rect.Y += lastParent.rectangle.Y;
                rect.X += lastParent.rectangle.X;
                if (lastParent != lastParent.parent)
                {
                    lastParent = lastParent.parent;
                }
                else
                {
                    break;
                }
            }
            return rect;
        }
        public virtual void updateInputs(int overlayWindowX, int overlayWindowY, bool inputEnabled) { }
        public virtual void drawElement() { }
        public virtual void Dispose() { }
        public virtual OverlayElement AddChildElement(OverlayElement child)
        {
            child.parent = this;
            children.Add(child);
            return children.Last();
        }
        public virtual bool OnWindowMessage(WindowMessage message, IntPtr wParam, IntPtr lParam)
        {
            if (!elementEnabled)
                return false;
            bool mouseOverChild = false;
            if (message == WindowMessage.Mousemove)
            {                
                mousePosition.X = WindowNativeMethods.GET_X_LPARAM(lParam);
                mousePosition.Y = WindowNativeMethods.GET_Y_LPARAM(lParam);

                foreach (var child in children)
                {
                    Rect rectAbs = child.getAbsolutePosition();
                    Rect fontAbs = child.getTextAbsolutePosition();
                    child.mousePosition = mousePosition;
                    if (rectAbs.Contains(mousePosition) || (child.includeFontRectInMouseOver && fontAbs.Contains(mousePosition)))
                    {
                        child.mouseOver = true;
                        mouseOverChild = true;
                    }
                    else
                    {
                        child.mouseOver = false;
                    }
                }
            }
            if(!mouseOverChild)
            {
                if (getAbsolutePosition().Contains(mousePosition) || (includeFontRectInMouseOver && getTextAbsolutePosition().Contains(mousePosition)))
                {
                    mouseOver = true;
                }
                else
                {
                    mouseOver = false;
                }
            }
            else
            {
                mouseOver = false;
            }
            foreach (var child in children)
            {
                if(child.OnWindowMessage(message, wParam, lParam))
                {
                    return true;
                }
            }
            return mouseOver;
        }
    }
}
