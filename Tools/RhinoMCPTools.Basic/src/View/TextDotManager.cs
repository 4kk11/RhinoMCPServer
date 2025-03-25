using System;
using System.Collections.Generic;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;

namespace RhinoMCPTools.Basic
{
    // TextDotを管理し、Dispose時に自動的に削除するためのクラス
    class TextDotManager : IDisposable
    {
        private RhinoDoc _doc;
        private List<Guid> _textDotIds;

        public TextDotManager(RhinoDoc doc)
        {
            _doc = doc;
            _textDotIds = new List<Guid>();
        }

        public Guid AddTextDot(Point3d location, string text, int size, ObjectAttributes attributes)
        {
            var textDot = new TextDot(text, location);
            textDot.FontHeight = size;
            Guid id = _doc.Objects.AddTextDot(textDot, attributes);
            _textDotIds.Add(id);
            this._doc.Views.Redraw();
            return id;
        }

        public void RemoveTextDot(Guid id)
        {
            _doc.Objects.Delete(id, true);
            _textDotIds.Remove(id);
            this._doc.Views.Redraw();
        }

        public bool Contains(Guid id)
        {
            return _textDotIds.Contains(id);
        }

        public int GetIndex(Guid id)
        {
            return _textDotIds.IndexOf(id);
        }

        public void Dispose()
        {
            foreach (Guid id in _textDotIds)
            {
                _doc.Objects.Delete(id, true);
            }
            RhinoApp.WriteLine("TextDotManager: Dispose");
            _textDotIds.Clear();
            _doc.Views.Redraw();
        }
    }
}