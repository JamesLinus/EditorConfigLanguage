﻿using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Language.StandardClassification;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Utilities;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;

namespace EditorConfig
{
    [Export(typeof(ICompletionSourceProvider))]
    [ContentType(ContentTypes.EditorConfig)]
    [Name("Editor Config")]
    public class EditorConfigCompletionSourceProvider : ICompletionSourceProvider
    {
        [Import]
        IClassifierAggregatorService ClassifierAggregatorService { get; set; }

        [Import]
        ITextStructureNavigatorSelectorService NavigatorService { get; set; }

        public ICompletionSource TryCreateCompletionSource(ITextBuffer textBuffer)
        {
            return new EditorConfigCompletionSource(textBuffer, ClassifierAggregatorService, NavigatorService);
        }
    }

    class EditorConfigCompletionSource : ICompletionSource
    {
        private ITextBuffer _buffer;
        private bool _disposed = false;
        private IClassifier _classifier;
        private ITextStructureNavigatorSelectorService _navigator;

        public EditorConfigCompletionSource(ITextBuffer buffer, IClassifierAggregatorService classifier, ITextStructureNavigatorSelectorService navigator)
        {
            _buffer = buffer;
            _classifier = classifier.GetClassifier(buffer);
            _navigator = navigator;
        }

        public void AugmentCompletionSession(ICompletionSession session, IList<CompletionSet> completionSets)
        {
            if (_disposed)
                return;

            ITextSnapshot snapshot = _buffer.CurrentSnapshot;
            var triggerPoint = session.GetTriggerPoint(snapshot);

            if (triggerPoint == null)
                return;

            SnapshotSpan extent = FindTokenSpanAtPosition(session).GetSpan(snapshot);
            var line = triggerPoint.Value.GetContainingLine().Extent;

            var spans = _classifier.GetClassificationSpans(line);

            var list = new List<Completion3>();
            string current = string.Empty;

            foreach (var span in spans)
            {
                if (span.ClassificationType.IsOfType(PredefinedClassificationTypeNames.Keyword))
                {
                    current = span.Span.GetText();

                    if (!span.Span.Contains(extent))
                        continue;

                    foreach (var key in CompletionItem.Items)
                        list.Add(CreateCompletion(key.Name, key.Description));
                }
                else if (span.ClassificationType.IsOfType(PredefinedClassificationTypeNames.SymbolDefinition))
                {
                    if (!span.Span.Contains(extent))
                        continue;

                    CompletionItem item = CompletionItem.GetCompletionItem(current);
                    if (item != null)
                    {
                        foreach (var value in item.Values)
                            list.Add(CreateCompletion(value));
                    }
                }
            }

            var applicableTo = snapshot.CreateTrackingSpan(extent, SpanTrackingMode.EdgeInclusive);

            completionSets.Add(new CompletionSet("All", "All", applicableTo, list, Enumerable.Empty<Completion3>()));
        }

        private Completion3 CreateCompletion(string name, string description = null)
        {            
            return new Completion3(name, name, description, KnownMonikers.Property, null);
        }

        private ITrackingSpan FindTokenSpanAtPosition(ICompletionSession session)
        {
            SnapshotPoint currentPoint = (session.TextView.Caret.Position.BufferPosition) - 1;
            ITextStructureNavigator navigator = _navigator.GetTextStructureNavigator(_buffer);
            TextExtent extent = navigator.GetExtentOfWord(currentPoint);
            return currentPoint.Snapshot.CreateTrackingSpan(extent.Span, SpanTrackingMode.EdgeInclusive);
        }

        public void Dispose()
        {
            _disposed = true;
        }
    }
}