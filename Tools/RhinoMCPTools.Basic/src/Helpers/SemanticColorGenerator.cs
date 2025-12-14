using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace RhinoMCPTools.Basic.Helpers
{
    /// <summary>
    /// element_type値から再現可能なセマンティックカラーを生成する
    ///
    /// 責務:
    /// - 文字列からRGB色への決定論的マッピング
    /// - 色の区別しやすさの確保
    /// </summary>
    public static class SemanticColorGenerator
    {
        /// <summary>
        /// 一般的なBIM要素用の予約色
        /// </summary>
        private static readonly Dictionary<string, Color> ReservedColors = new()
        {
            ["wall"] = Color.FromArgb(255, 128, 128),      // 薄赤
            ["floor"] = Color.FromArgb(128, 128, 255),     // 薄青
            ["ceiling"] = Color.FromArgb(255, 255, 128),   // 薄黄
            ["door"] = Color.FromArgb(255, 128, 0),        // オレンジ
            ["window"] = Color.FromArgb(0, 255, 255),      // シアン
            ["column"] = Color.FromArgb(128, 0, 128),      // 紫
            ["beam"] = Color.FromArgb(0, 128, 0),          // 緑
            ["stair"] = Color.FromArgb(255, 0, 255),       // マゼンタ
            ["roof"] = Color.FromArgb(128, 64, 0),         // 茶色
            ["furniture"] = Color.FromArgb(255, 192, 203), // ピンク
            ["bookshelf"] = Color.FromArgb(139, 69, 19),   // サドルブラウン
            ["sofa"] = Color.FromArgb(70, 130, 180),       // スチールブルー
            ["table"] = Color.FromArgb(210, 180, 140),     // タン
            ["chair"] = Color.FromArgb(144, 238, 144),     // ライトグリーン
        };

        /// <summary>
        /// 黄金比の共役（均等な色分布に使用）
        /// </summary>
        private const double GoldenRatioConjugate = 0.618033988749895;

        /// <summary>
        /// element_type値のコレクションから色マッピングを生成
        /// </summary>
        /// <param name="elementTypes">element_type値のリスト</param>
        /// <returns>element_typeから色へのマッピング</returns>
        public static IReadOnlyDictionary<string, Color> GenerateColorMapping(IReadOnlyList<string> elementTypes)
        {
            if (elementTypes == null || elementTypes.Count == 0)
            {
                return new Dictionary<string, Color>();
            }

            var result = new Dictionary<string, Color>();
            var unreservedTypes = new List<string>();

            // 1. 予約済み色を先に割り当て
            foreach (var elementType in elementTypes.Distinct())
            {
                var normalized = elementType.ToLowerInvariant().Trim();
                if (ReservedColors.TryGetValue(normalized, out var reservedColor))
                {
                    result[elementType] = reservedColor;
                }
                else
                {
                    unreservedTypes.Add(elementType);
                }
            }

            // 2. 残りの型にはHSL色相で均等分布（ゴールデンレシオ使用）
            for (int i = 0; i < unreservedTypes.Count; i++)
            {
                var hue = ((i * GoldenRatioConjugate) % 1.0) * 360.0;
                var color = HslToRgb(hue, 0.7, 0.5); // 彩度70%, 明度50%
                result[unreservedTypes[i]] = color;
            }

            return result;
        }

        /// <summary>
        /// HSL to RGB変換
        /// </summary>
        /// <param name="h">色相 (0-360)</param>
        /// <param name="s">彩度 (0-1)</param>
        /// <param name="l">明度 (0-1)</param>
        /// <returns>RGB Color</returns>
        private static Color HslToRgb(double h, double s, double l)
        {
            double c = (1 - Math.Abs(2 * l - 1)) * s;
            double x = c * (1 - Math.Abs((h / 60) % 2 - 1));
            double m = l - c / 2;

            double r, g, b;
            if (h < 60) { r = c; g = x; b = 0; }
            else if (h < 120) { r = x; g = c; b = 0; }
            else if (h < 180) { r = 0; g = c; b = x; }
            else if (h < 240) { r = 0; g = x; b = c; }
            else if (h < 300) { r = x; g = 0; b = c; }
            else { r = c; g = 0; b = x; }

            return Color.FromArgb(
                (int)((r + m) * 255),
                (int)((g + m) * 255),
                (int)((b + m) * 255)
            );
        }

        /// <summary>
        /// ColorをHex文字列に変換
        /// </summary>
        public static string ToHexString(Color color)
        {
            return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        }
    }
}
