// <file>
//     <copyright see="prj:///doc/copyright.txt"/>
//     <license see="prj:///doc/license.txt"/>
//     <author name="Daniel Grunwald"/>
//     <version>$Revision: 4686 $</version>
// </file>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.TextFormatting;

using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;
using ICSharpCode.AvalonEdit.Utils;

namespace ICSharpCode.AvalonEdit.Editing
{
	/// <summary>
	/// A margin that shows markers for foldings and allows to expand/collapse the foldings.
	/// </summary>
	public class FoldingMargin : AbstractMargin
	{
		/// <summary>
		/// Gets/Sets the folding manager from which the foldings should be shown.
		/// </summary>
		public FoldingManager FoldingManager { get; set; }
		
		internal const double SizeFactor = Constants.PixelPerPoint;
		
		/// <inheritdoc/>
		protected override Size MeasureOverride(Size availableSize)
		{
			foreach (FoldingMarginMarker m in markers) {
				m.Measure(availableSize);
			}
			return new Size(SizeFactor * (double)GetValue(TextBlock.FontSizeProperty), 0);
		}
		
		/// <inheritdoc/>
		protected override Size ArrangeOverride(Size finalSize)
		{
			foreach (FoldingMarginMarker m in markers) {
				int visualColumn = m.VisualLine.GetVisualColumn(m.FoldingSection.StartOffset - m.VisualLine.FirstDocumentLine.Offset);
				TextLine textLine = m.VisualLine.GetTextLine(visualColumn);
				double yPos = m.VisualLine.GetTextLineVisualYPosition(textLine, VisualYPosition.LineTop) - TextView.VerticalOffset;
				yPos += (textLine.Height - m.DesiredSize.Height) / 2;
				double xPos = (finalSize.Width - m.DesiredSize.Width) / 2;
				m.Arrange(new Rect(new Point(xPos, yPos), m.DesiredSize));
			}
			return base.ArrangeOverride(finalSize);
		}
		
		/// <inheritdoc/>
		protected override void OnTextViewChanged(TextView oldTextView, TextView newTextView)
		{
			if (oldTextView != null) {
				oldTextView.VisualLinesChanged -= TextViewVisualLinesChanged;
			}
			base.OnTextViewChanged(oldTextView, newTextView);
			if (newTextView != null) {
				newTextView.VisualLinesChanged += TextViewVisualLinesChanged;
			}
			TextViewVisualLinesChanged(null, null);
		}
		
		List<FoldingMarginMarker> markers = new List<FoldingMarginMarker>();
		
		void TextViewVisualLinesChanged(object sender, EventArgs e)
		{
			foreach (FoldingMarginMarker m in markers) {
				RemoveVisualChild(m);
			}
			markers.Clear();
			InvalidateVisual();
			if (TextView != null && FoldingManager != null && TextView.VisualLinesValid) {
				foreach (VisualLine line in TextView.VisualLines) {
					FoldingSection fs = FoldingManager.GetNextFolding(line.FirstDocumentLine.Offset);
					if (fs == null)
						continue;
					if (fs.StartOffset <= line.LastDocumentLine.Offset + line.LastDocumentLine.Length) {
						FoldingMarginMarker m = new FoldingMarginMarker {
							IsExpanded = !fs.IsFolded,
							SnapsToDevicePixels = true,
							VisualLine = line,
							FoldingSection = fs
						};
						markers.Add(m);
						AddVisualChild(m);
						
						m.IsMouseDirectlyOverChanged += delegate { InvalidateVisual(); };
						
						InvalidateMeasure();
						continue;
					}
				}
			}
		}
		
		/// <inheritdoc/>
		protected override int VisualChildrenCount {
			get { return markers.Count; }
		}
		
		/// <inheritdoc/>
		protected override Visual GetVisualChild(int index)
		{
			return markers[index];
		}
		
		static readonly Pen grayPen = MakeFrozenPen(Brushes.Gray);
		static readonly Pen blackPen = MakeFrozenPen(Brushes.Black);
		
		static Pen MakeFrozenPen(Brush brush)
		{
			Pen pen = new Pen(brush, 1);
			pen.Freeze();
			return pen;
		}
		
		/// <inheritdoc/>
		protected override void OnRender(DrawingContext drawingContext)
		{
			if (!TextView.VisualLinesValid)
				return;
			if (TextView.VisualLines.Count == 0 || FoldingManager == null)
				return;
			
			var allTextLines = TextView.VisualLines.SelectMany(vl => vl.TextLines).ToList();
			Pen[] colors = new Pen[allTextLines.Count + 1];
			Pen[] endMarker = new Pen[allTextLines.Count];
			
			CalculateFoldLinesForFoldingsActiveAtStart(allTextLines, colors, endMarker);
			CalculateFoldLinesForMarkers(allTextLines, colors, endMarker);
			DrawFoldLines(drawingContext, colors, endMarker);
			
			base.OnRender(drawingContext);
		}

		/// <summary>
		/// Calculates fold lines for all folding sections that start in front of the current view
		/// and run into the current view.
		/// </summary>
		void CalculateFoldLinesForFoldingsActiveAtStart(List<TextLine> allTextLines, Pen[] colors, Pen[] endMarker)
		{
			int viewStartOffset = TextView.VisualLines[0].FirstDocumentLine.Offset;
			int viewEndOffset = TextView.VisualLines.Last().LastDocumentLine.EndOffset;
			var foldings = FoldingManager.GetFoldingsContaining(viewStartOffset);
			int maxEndOffset = 0;
			foreach (FoldingSection fs in foldings) {
				int end = fs.EndOffset;
				if (end < viewEndOffset && !fs.IsFolded) {
					int textLineNr = GetTextLineIndexFromOffset(allTextLines, end);
					if (textLineNr >= 0) {
						endMarker[textLineNr] = grayPen;
					}
				}
				if (end > maxEndOffset && fs.StartOffset < viewStartOffset) {
					maxEndOffset = end;
				}
			}
			if (maxEndOffset > 0) {
				if (maxEndOffset > viewEndOffset) {
					for (int i = 0; i < colors.Length; i++) {
						colors[i] = grayPen;
					}
				} else {
					int maxTextLine = GetTextLineIndexFromOffset(allTextLines, maxEndOffset);
					for (int i = 0; i <= maxTextLine; i++) {
						colors[i] = grayPen;
					}
				}
			}
		}
		
		/// <summary>
		/// Calculates fold lines for all folding sections that start inside the current view
		/// </summary>
		void CalculateFoldLinesForMarkers(List<TextLine> allTextLines, Pen[] colors, Pen[] endMarker)
		{
			foreach (FoldingMarginMarker marker in markers) {
				int end = marker.FoldingSection.EndOffset;
				int endTextLineNr = GetTextLineIndexFromOffset(allTextLines, end);
				if (!marker.FoldingSection.IsFolded && endTextLineNr >= 0) {
					if (marker.IsMouseDirectlyOver)
						endMarker[endTextLineNr] = blackPen;
					else if (endMarker[endTextLineNr] == null)
						endMarker[endTextLineNr] = grayPen;
				}
				int startTextLineNr = GetTextLineIndexFromOffset(allTextLines, marker.FoldingSection.StartOffset);
				if (startTextLineNr >= 0) {
					for (int i = startTextLineNr + 1; i < colors.Length && i - 1 != endTextLineNr; i++) {
						if (marker.IsMouseDirectlyOver)
							colors[i] = blackPen;
						else if (colors[i] == null)
							colors[i] = grayPen;
					}
				}
			}
		}
		
		/// <summary>
		/// Draws the lines for the folding sections (vertical line with 'color', horizontal lines with 'endMarker')
		/// Each entry in the input arrays corresponds to one TextLine.
		/// </summary>
		void DrawFoldLines(DrawingContext drawingContext, Pen[] colors, Pen[] endMarker)
		{
			double markerXPos = Math.Round(RenderSize.Width / 2);
			double startY = 0;
			Pen currentPen = colors[0];
			int tlNumber = 0;
			foreach (VisualLine vl in TextView.VisualLines) {
				foreach (TextLine tl in vl.TextLines) {
					if (endMarker[tlNumber] != null) {
						double visualPos = GetVisualPos(vl, tl);
						drawingContext.DrawLine(endMarker[tlNumber], new Point(markerXPos, visualPos), new Point(RenderSize.Width, visualPos));
					}
					if (colors[tlNumber + 1] != currentPen) {
						double visualPos = GetVisualPos(vl, tl);
						if (currentPen != null) {
							drawingContext.DrawLine(currentPen, new Point(markerXPos, startY), new Point(markerXPos, visualPos));
						}
						currentPen = colors[tlNumber + 1];
						startY = visualPos;
					}
					tlNumber++;
				}
			}
			if (currentPen != null) {
				drawingContext.DrawLine(currentPen, new Point(markerXPos, startY), new Point(markerXPos, RenderSize.Height));
			}
		}
		
		double GetVisualPos(VisualLine vl, TextLine tl)
		{
			double pos = vl.GetTextLineVisualYPosition(tl, VisualYPosition.LineTop) + tl.Height / 2 - TextView.VerticalOffset;
			return Math.Round(pos) + 0.5;
		}
		
		int GetTextLineIndexFromOffset(List<TextLine> textLines, int offset)
		{
			int lineNumber = TextView.Document.GetLineByOffset(offset).LineNumber;
			VisualLine vl = TextView.GetVisualLine(lineNumber);
			if (vl != null) {
				int relOffset = offset - vl.FirstDocumentLine.Offset;
				TextLine line = vl.GetTextLine(vl.GetVisualColumn(relOffset));
				return textLines.IndexOf(line);
			}
			return -1;
		}
	}
}
