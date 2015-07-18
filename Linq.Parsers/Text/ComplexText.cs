﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Linq.Parsers
{
    internal class ComplexText : Text, IEnumerable<char>
    {
        #region Fields

        internal List<Text> texts;
        private int length;
        private bool isLengthEvaluated = false;

        #endregion //Fields

        #region Properties

        #region Length

        public override int Length
        {
            get
            {
                if (this.isLengthEvaluated)
                    return this.length;

                this.length = this.texts.Sum(t => t.Length);
                this.isLengthEvaluated = true;
                return this.length;
            }
        }

        #endregion //Length

        #region Indexer

        public override char this[int index]
        {
            get
            {
                if (index >= this.length)
                    throw new IndexOutOfRangeException("The index is out of bounds.");

                foreach (var text in texts)
                {
                    var textLength = text.Length;
                    if (index < textLength)
                        return text[index];

                    index -= textLength;
                }

                throw new IndexOutOfRangeException("The index is out of bounds.");
            }
        }

        #endregion //Indexer

        #endregion //Properties

        #region Constructors

        public ComplexText(params Text[] texts)
            : this((IEnumerable<Text>)texts, true)
        { }

        public ComplexText(IEnumerable<Text> texts, bool enableOptimizations = true)
        {
            texts.AssertNotNull();
            this.texts =
                enableOptimizations ?
                FlattenTextOptimally(texts) :
                FlattenText(texts);
        }

        #endregion //Constructors

        #region IEnumerable Members

        public override IEnumerator<char> GetEnumerator()
        {
            foreach (var text in this.texts)
                foreach (var character in text)
                    yield return character;
        }

        #endregion //IEnumerable Members

        #region Methods

        #region Split

        public override SplitText Split(int index)
        {
            var texts = this.texts;

            var headList = new List<Text>();
            foreach (var text in texts)
            {
                if (index < text.Length)
                    break;

                headList.Add(text);
                index -= text.Length;
            }

            var split = texts[headList.Count].Split(index);
            headList.Add(split.Head);

            var tailList = new[] { split.Tail }.Concat(texts.Skip(headList.Count));

            return new SplitText(
                new ComplexText(headList, enableOptimizations: false),
                new ComplexText(tailList, enableOptimizations: false));
        }

        #endregion //Split

        #region IsSimpleTextAppendableTo

        internal override bool IsSimpleTextAppendableTo(SimpleText tail)
        {
            Debug.Assert(tail != null);
            Debug.Assert(tail.Length > 0);
            Debug.Assert(this.Length > 0);

            var lastComponent = this.texts[this.texts.Count - 1] as SimpleText;
            return
                lastComponent != null &&
                lastComponent.IsSimpleTextAppendableTo(tail);
        }

        #endregion //IsSimpleTextAppendableTo

        #region IsComplexTextAppendableTo

        internal override bool IsComplexTextAppendableTo(ComplexText tail)
        {
            Debug.Assert(tail != null);
            Debug.Assert(tail.Length > 0);
            Debug.Assert(this.Length > 0);

            var lastComponent = this.texts[this.texts.Count - 1] as SimpleText;
            var firstTailComponent = tail.texts[0] as SimpleText;

            return
                lastComponent != null &&
                firstTailComponent != null &&
                lastComponent.IsSimpleTextAppendableTo(firstTailComponent);
        }

        #endregion //IsComplexTextAppendableTo

        #region AppendSimpleText

        internal override Text AppendSimpleText(SimpleText tail)
        {
            Debug.Assert(tail != null);
            Debug.Assert(tail.Length > 0);
            Debug.Assert(this.Length > 0);

            if (IsSimpleTextAppendableTo(tail))
                return new ComplexText(
                    this.texts.Take(this.texts.Count - 1)
                    .Concat(new[] { this.texts.Last().AppendSimpleText(tail) }),
                    enableOptimizations: false);

            return new ComplexText(
                this.texts.Concat(new[] { tail }),
                enableOptimizations: false);
        }

        #endregion //AppendSimpleText

        #region AppendComplexText

        internal override Text AppendComplexText(ComplexText tail)
        {
            Debug.Assert(tail != null);
            Debug.Assert(tail.Length > 0);
            Debug.Assert(this.Length > 0);

            if (IsComplexTextAppendableTo(tail))
                return new ComplexText(
                    this.texts.Take(this.texts.Count - 1)
                    .Concat(new[] { this.texts.Last().AppendSimpleText((SimpleText)tail.texts.First()) })
                    .Concat(tail.texts.Skip(1)),
                    enableOptimizations: false);

            return new ComplexText(
                this.texts.Concat(tail.texts),
                enableOptimizations: false);
        }

        #endregion //AppendComplexText

        #region Evaluate

        internal override string Evaluate()
        {
            var builder = new StringBuilder();
            foreach (var text in this.texts)
                builder.Append(text.ToString());

            return builder.ToString();
        }

        #endregion //Evaluate

        #endregion //Methods

        #region Utilities

        #region FlattenText

        private static List<Text> FlattenText(IEnumerable<Text> texts)
        {
            var result = new List<Text>();

            foreach (var text in texts)
            {
                var complexText = text as ComplexText;
                if (complexText == null)
                {
                    result.Add(text);
                    continue;
                }

                foreach (var innerText in complexText.texts)
                    result.Add(innerText);
            }

            return result;
        }

        #endregion //FlattenText

        #region FlattenTextOptimally

        private static List<Text> FlattenTextOptimally(IEnumerable<Text> texts)
        {
            var result = new List<Text>();

            Text lastText = null;
            foreach (var text in texts)
            {
                if (text == null || text.Length == 0)
                    continue;

                var complexText = text as ComplexText;
                if (complexText == null)
                {
                    if (lastText != null)
                    {
                        var simpleText = (SimpleText)text;
                        if (lastText.IsSimpleTextAppendableTo(simpleText))
                        {
                            lastText = lastText.AppendSimpleText(simpleText);
                            result[result.Count - 1] = lastText;
                            continue;
                        }
                    }

                    result.Add(text);
                    lastText = text;
                    continue;
                }

                var firstPass = true;
                foreach (var innerText in complexText.texts)
                {
                    if (!firstPass || lastText == null)
                    {
                        result.Add(innerText);
                        continue;
                    }

                    firstPass = false;
                    var simpleInnerText = innerText as SimpleText;
                    if (simpleInnerText != null)
                    {
                        if (lastText.IsSimpleTextAppendableTo(simpleInnerText))
                        {
                            result[result.Count - 1] = lastText.AppendSimpleText(simpleInnerText);
                            continue;
                        }
                    }
                    else
                    {
                        var complexInnerText = (ComplexText)innerText;
                        if (lastText.IsComplexTextAppendableTo(complexInnerText))
                        {
                            result[result.Count - 1] = lastText.AppendComplexText(complexInnerText);
                            continue;
                        }
                    }
                }

                if (result.Count > 0)
                    lastText = result[result.Count - 1];
            }

            return result;
        }

        #endregion //FlattenTextOptimally

        #endregion //Utilities
    }
}
