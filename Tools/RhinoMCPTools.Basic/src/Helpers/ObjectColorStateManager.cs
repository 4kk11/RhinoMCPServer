using System;
using System.Collections.Generic;
using System.Drawing;
using Rhino;
using Rhino.DocObjects;

namespace RhinoMCPTools.Basic.Helpers
{
    /// <summary>
    /// オブジェクトの色状態を一時的に変更し、Dispose時に復元する
    /// TextDotManagerパターンに従ったRAII実装
    ///
    /// 責務:
    /// - 元の色状態を保存
    /// - 一時的な色の適用
    /// - Dispose時の自動復元
    /// </summary>
    public class ObjectColorStateManager : IDisposable
    {
        private readonly RhinoDoc _doc;
        private readonly Dictionary<Guid, (Color OriginalColor, ObjectColorSource OriginalSource)> _savedStates;
        private bool _disposed;

        public ObjectColorStateManager(RhinoDoc doc)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
            _savedStates = new Dictionary<Guid, (Color, ObjectColorSource)>();
        }

        /// <summary>
        /// オブジェクトの色を一時的に変更（元の状態を保存）
        /// </summary>
        /// <param name="obj">対象オブジェクト</param>
        /// <param name="color">設定する色</param>
        public void SetTemporaryColor(RhinoObject obj, Color color)
        {
            if (obj == null)
            {
                return;
            }

            // 状態を保存（まだ保存していない場合のみ）
            if (!_savedStates.ContainsKey(obj.Id))
            {
                _savedStates[obj.Id] = (
                    obj.Attributes.ObjectColor,
                    obj.Attributes.ColorSource
                );
            }

            // 新しい色を適用
            obj.Attributes.ObjectColor = color;
            obj.Attributes.ColorSource = ObjectColorSource.ColorFromObject;
            obj.CommitChanges();
        }

        /// <summary>
        /// 複数オブジェクトの色を一括で変更
        /// </summary>
        /// <param name="assignments">オブジェクトと色のペアのリスト</param>
        public void SetTemporaryColors(IEnumerable<(RhinoObject Obj, Color Color)> assignments)
        {
            if (assignments == null)
            {
                return;
            }

            foreach (var (obj, color) in assignments)
            {
                SetTemporaryColor(obj, color);
            }
        }

        /// <summary>
        /// 変更されたオブジェクトの数
        /// </summary>
        public int ChangedObjectCount => _savedStates.Count;

        /// <summary>
        /// 全オブジェクトを元の状態に復元
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            foreach (var (id, (originalColor, originalSource)) in _savedStates)
            {
                var obj = _doc.Objects.Find(id);
                if (obj != null)
                {
                    obj.Attributes.ObjectColor = originalColor;
                    obj.Attributes.ColorSource = originalSource;
                    obj.CommitChanges();
                }
            }

            _savedStates.Clear();
            _doc.Views.Redraw();
            RhinoApp.WriteLine("ObjectColorStateManager: Colors restored");
            _disposed = true;
        }
    }
}
