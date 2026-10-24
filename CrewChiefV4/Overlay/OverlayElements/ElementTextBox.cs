using GameOverlay.Drawing;
using GameOverlay.PInvoke;
using System;
using System.Windows;
using System.Windows.Forms;

namespace CrewChiefV4.Overlay
{
    public class ElementTextBox : OverlayElement
    {
        bool acceptInput = false;
        public string text = "";
        public int maxTextLength = 0;
        public bool digitsOnly = false;
        public bool internalDrawBox = true;
        public TextAlign textAlign = TextAlign.Left | TextAlign.Top;
        public ElementTextBox(Graphics gfx, string elementTitle, Font font, Rect rectangle,
            ColorScheme colorScheme, string text = "", bool initialEnableState = true, bool acceptInput = false, int maxTextLength = 0, 
            bool digitsOnly = false, TextAlign textAlign = TextAlign.Left | TextAlign.Top, bool internalDrawBox = true) :
            base(gfx, elementTitle, font, rectangle, colorScheme, initialEnableState)
        {
            this.text = text;
            this.acceptInput = acceptInput;
            this.maxTextLength = maxTextLength;
            this.digitsOnly = digitsOnly;
            this.textAlign = textAlign;
            this.internalDrawBox = internalDrawBox;
        }
        public override void drawElement()
        {
            if (!this.elementEnabled)
                return;
            System.Windows.Rect rect = getAbsolutePosition();
            if(internalDrawBox)
            {
                gfx.DrawBox2D(base.secondaryBrush, base.primaryBrush, new Rectangle(rect), (mouseOver || selected) && acceptInput ? 2 : 1);
            }
            
            base.OnElementDraw?.Invoke(this, new OverlayElementDrawUpdate(gfx, new Rectangle(rect)));

            foreach (var child in children)
            {
                child.drawElement();
            }
            return;
        }
        public override bool OnWindowMessage(WindowMessage message, IntPtr wParam, IntPtr lParam)
        {
            //base.OnWindowMessage(message, wParam, lParam);
            if(message == WindowMessage.Keydown && acceptInput && selected)
            {
                if((Keys)wParam == Keys.Return || (Keys)wParam == Keys.Enter)
                {
                    OnEnterKeyDown?.Invoke(this, new OverlayElementClicked(null));
                }
                else
                {
                    this.OnKeyDown?.Invoke(this, new OverlayElementKeyDown(gfx, (Keys)wParam));
                }
                return true;
            }
            if (mouseOver && acceptInput)
            {
                if (message == WindowMessage.Lbuttondown)
                {
                    mousePressed = true;
                }
                if (message == WindowMessage.Lbuttonup && mousePressed)
                {
                    mousePressed = false;
                }
                return true;
            }
            else
            {
                mousePressed = false;
            }
            return false;
        }
    }
}
