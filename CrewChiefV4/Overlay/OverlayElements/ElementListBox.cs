using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using GameOverlay.Drawing;
using GameOverlay.PInvoke;
using System;
using System.Windows;
using System.Windows.Forms;

namespace CrewChiefV4.Overlay
{
    public class ElementListBox : OverlayElement
    {
        public string[] objects = null;
        public int selectedObjectIndex = 0;
        public int startObjectIndex = 0;
        private float lineHeight;
        private float sliderHeightSteps;
        private int elementsVisible = 0;
        private bool hasVerticalScrollBar = false;
        private double cursorOffsetInRectY = 0;
        Rect verticalScrollBarRect = new Rect();
        Rect verticalScrollBarArrowUpRect = new Rect();
        Rect verticalScrollBarArrowDownRect = new Rect();
        Rect verticalScrollBarSliderRect = new Rect();
        public EventHandler<OverlayElementClicked> OnSelectedObjectChanged;
        public ElementListBox(Graphics gfx, string elementTitle, Font font, Rect rectangle,
            ColorScheme colorScheme, string[]objects = null, string selectedObject = null, bool initialEnable = true) :
            base(gfx, elementTitle, font, rectangle, colorScheme, initialEnable)
        {
            if (objects == null)
                return;

            this.objects = objects;
            this.lineHeight = font.MeasureString(objects[0]).Height;
            
            elementsVisible = (int)Math.Floor(rectangle.Height / lineHeight);

            AddVerticalScrollBar();
            MoveToSelection(selectedObject);
        }
        private void AddVerticalScrollBar()
        {     
            if(elementsVisible < objects.Length)
            {
                System.Windows.Rect rect = getAbsolutePosition();
                rectangle.Width -= 15;                
                verticalScrollBarRect.X = rect.X + rectangle.Width;
                verticalScrollBarRect.Width = 15;
                verticalScrollBarRect.Y = rect.Y;
                verticalScrollBarRect.Height = rect.Height;

                verticalScrollBarArrowUpRect.X = rect.X + rectangle.Width;
                verticalScrollBarArrowUpRect.Y = rect.Y;
                verticalScrollBarArrowUpRect.Width = 15;
                verticalScrollBarArrowUpRect.Height = 15;

                verticalScrollBarArrowDownRect.X = rect.X + rectangle.Width;
                verticalScrollBarArrowDownRect.Y = rect.Bottom - 15;
                verticalScrollBarArrowDownRect.Width = 15;
                verticalScrollBarArrowDownRect.Height = 15;

                verticalScrollBarSliderRect.X = rect.X + rectangle.Width;
                verticalScrollBarSliderRect.Y = verticalScrollBarArrowUpRect.Bottom;
                verticalScrollBarSliderRect.Width = 15;

                var sliderMaxHeight = verticalScrollBarRect.Height - (verticalScrollBarArrowUpRect.Height + verticalScrollBarArrowDownRect.Height);
                sliderHeightSteps = (float)sliderMaxHeight / objects.Length;
                verticalScrollBarSliderRect.Height = sliderHeightSteps * elementsVisible;

                hasVerticalScrollBar = true;
            }
        }
        private void MoveToSelection(string obj)
        {
            if (obj != null)
            {
                selectedObjectIndex = objects.ToList().IndexOf(obj);
                if (selectedObjectIndex == -1)
                    selectedObjectIndex = 0;
                if (selectedObjectIndex + 1 > elementsVisible)
                    startObjectIndex = Math.Abs((selectedObjectIndex + 1) - elementsVisible);
            }
        }
        public void AddObject(string obj)
        {
            if(objects == null)
            {
                objects = new string[1] { obj };
            }
            else
            {
                List<string> objs = objects.ToList();
                objs.Add(obj);
                objects = objs.ToArray();
            }

            var sliderMaxHeight = verticalScrollBarRect.Height - (verticalScrollBarArrowUpRect.Height + verticalScrollBarArrowDownRect.Height);
            sliderHeightSteps = (float)sliderMaxHeight / objects.Length;
            verticalScrollBarSliderRect.Height = sliderHeightSteps * elementsVisible;
            AddVerticalScrollBar();
            MoveToSelection(obj);
            OnSelectedObjectChanged?.Invoke(this, new OverlayElementClicked(gfx, costumTextId: objects[selectedObjectIndex]));
        }

        public string GetSelectedObject()
        {
            return objects == null ? null : objects[selectedObjectIndex];
        }

        public override void drawElement()
        {
            if (!this.elementEnabled)
                return;
            System.Windows.Rect rect = getAbsolutePosition();            
            gfx.DrawBox2D(base.secondaryBrush, base.primaryBrush, new Rectangle(rect), mouseOver || selected ? 1.5f : 1);
            if (hasVerticalScrollBar)
            {
                verticalScrollBarRect.X = rect.X + rectangle.Width;
                verticalScrollBarRect.Y = rect.Y;

                verticalScrollBarArrowUpRect.X = rect.X + rectangle.Width;
                verticalScrollBarArrowUpRect.Y = rect.Y;

                verticalScrollBarArrowDownRect.X = rect.X + rectangle.Width;
                verticalScrollBarArrowDownRect.Y = rect.Bottom - 15;

                verticalScrollBarSliderRect.X = rect.X + rectangle.Width;
                verticalScrollBarSliderRect.Y = verticalScrollBarArrowUpRect.Bottom + (sliderHeightSteps * startObjectIndex);

                gfx.DrawBox2D(base.secondaryBrush, base.primaryBrush, new Rectangle(verticalScrollBarRect), mouseOver || selected ? 1.5f : 1);
                gfx.DrawBox2D(base.secondaryBrush, base.primaryBrush, new Rectangle(verticalScrollBarArrowUpRect), mouseOver || selected ? 1.5f : 1);
                gfx.DrawBox2D(base.secondaryBrush, base.primaryBrush, new Rectangle(verticalScrollBarArrowDownRect), mouseOver || selected ? 1.5f : 1);

                gfx.FillTriangle(base.secondaryBrush, (float)verticalScrollBarArrowUpRect.X + 3.5f, (float)verticalScrollBarArrowUpRect.Bottom - 4,
                    (float)verticalScrollBarArrowUpRect.X + 11.5f, (float)verticalScrollBarArrowUpRect.Bottom - 4,
                    (float)verticalScrollBarArrowUpRect.X + 7.5f, (float)verticalScrollBarArrowUpRect.Y + 4);

                gfx.FillTriangle(base.secondaryBrush, (float)verticalScrollBarArrowDownRect.X + 3.5f, (float)verticalScrollBarArrowDownRect.Top + 4,
                    (float)verticalScrollBarArrowDownRect.X + 11.5f, (float)verticalScrollBarArrowDownRect.Top + 4,
                    (float)verticalScrollBarArrowDownRect.X + 7.5f, (float)verticalScrollBarArrowDownRect.Bottom - 4);

                gfx.FillRectangle(secondaryBrush, new Rectangle(verticalScrollBarSliderRect));
            }
            var lineOffsetY = 0f;
            for (int i = startObjectIndex, count = 1; i < objects.Length; i++, count++)
            {                
                if (count > elementsVisible)
                    break;

                Rect textRect = rect;
                textRect.Y = rect.Top + lineOffsetY + 1;
                textRect.Height = lineHeight - 1;

                if (i != selectedObjectIndex)
                {
                    gfx.DrawTextClipped(font, secondaryBrush, (float)textRect.X + 1, (float)textRect.Y, rect, objects[i]);
                }
                else
                {
                    gfx.FillRectangle(secondaryBrush, new Rectangle(textRect));                  
                    gfx.DrawTextClipped(font, primaryBrush, (float)textRect.X + 1, (float)textRect.Y, rect, objects[i]);
                }
                lineOffsetY += lineHeight;
            }

            base.OnElementDraw?.Invoke(this, new OverlayElementDrawUpdate(gfx, new Rectangle(rect)));

            return;
        }
        private void MoveUp()
        {
            if (selectedObjectIndex != 0 )
            {
                selectedObjectIndex--;
                if((selectedObjectIndex + 1) - startObjectIndex == 0 && startObjectIndex != 0)
                {
                    startObjectIndex--;
                }
                OnSelectedObjectChanged?.Invoke(this, new OverlayElementClicked(gfx, costumTextId: objects[selectedObjectIndex]));
            }
        }
        private void MoveDown()
        {
            if (selectedObjectIndex < objects.Length - 1)
            {
                selectedObjectIndex++;
                if (selectedObjectIndex + 1 + startObjectIndex > elementsVisible)
                {                    
                    startObjectIndex++;
                }
                OnSelectedObjectChanged?.Invoke(this, new OverlayElementClicked(gfx, costumTextId: objects[selectedObjectIndex]));
            }
        }
        private void ScrollUp()
        {
            if(startObjectIndex != 0)
            {
                startObjectIndex--;
            }
        }
        private void ScrollDown()
        {
            if (startObjectIndex + elementsVisible < objects.Length)
            {
                startObjectIndex++;
            }
        }
        public override bool OnWindowMessage(WindowMessage message, IntPtr wParam, IntPtr lParam)
        {
            //base.OnWindowMessage(message, wParam, lParam);
            if (message == WindowMessage.Keydown && selected)
            {
                if ((Keys)wParam == Keys.Return || (Keys)wParam == Keys.Enter)
                {
                    OnEnterKeyDown?.Invoke(this, new OverlayElementClicked(null));
                }
                if((Keys)wParam == Keys.Up)
                {
                    MoveUp();
                }
                if ((Keys)wParam == Keys.Down)
                {
                    MoveDown();
                }
                return true;
            }
            if (mouseOver || verticalScrollBarRect.Contains(mousePosition))
            {
                if (message == WindowMessage.Mousewheel)
                {
                    int UpDown = WindowNativeMethods.GET_WHEEL_DELTA_WPARAM(wParam);
                    if(UpDown < 0)
                    {
                        ScrollDown();
                    }
                    else
                    {
                        ScrollUp();
                    }
                    OnElementMWheel?.Invoke(this, new OverlayElementMouseWheel(gfx, UpDown: UpDown));
                }

                if (message == WindowMessage.Lbuttondown)
                {
                    if (!mousePressed)
                    {
                        cursorOffsetInRectY = mousePosition.Y;
                    }
                    mousePressed = true;
                    if (hasVerticalScrollBar)
                    {
                        if (verticalScrollBarArrowUpRect.Contains(mousePosition))
                        {
                            ScrollUp();
                        }
                        if (verticalScrollBarArrowDownRect.Contains(mousePosition))
                        {
                            ScrollDown();
                        }
                    }
                }
                if (verticalScrollBarSliderRect.Contains(mousePosition) && hasVerticalScrollBar)
                {
                    if (message == WindowMessage.Mousemove && mousePressed)
                    {
                        if(mousePosition.Y > cursorOffsetInRectY)
                        {
                            cursorOffsetInRectY = mousePosition.Y;
                            ScrollDown();
                        }
                        if (mousePosition.Y < cursorOffsetInRectY)
                        {
                            cursorOffsetInRectY = mousePosition.Y;
                            ScrollUp();
                        }
                    }
                }
                if (message == WindowMessage.Lbuttonup && mousePressed)
                {
                    var lineOffsetY = 0f;
                    Rect rect = getAbsolutePosition();
                    for (int i = startObjectIndex; i < objects.Length; i++)
                    {
                        Rect textRect = rect;
                        textRect.Height = lineHeight;
                        textRect.Y = rect.Top + lineOffsetY;
                        if (textRect.Contains(mousePosition))
                        {
                            selectedObjectIndex = i;
                            OnSelectedObjectChanged?.Invoke(this, new OverlayElementClicked(gfx, costumTextId: objects[selectedObjectIndex]));
                            break;
                        }
                        lineOffsetY += lineHeight;
                    }                   
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

