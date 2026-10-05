using System.Collections.Generic;
using System.Linq;
using PuzzleStudio.Core.Data;
using PuzzleStudio.Core.Puzzle;
using PuzzleStudio.Studio.App;
using PuzzleStudio.Studio.Widgets;
using UnityEngine.UIElements;

namespace PuzzleStudio.Studio.Panels
{
    public sealed class GameplayPanel : StudioPanel
    {
        public GameplayPanel(StudioApp app) : base(app) { }
        public override string Id => "gameplay";
        public override string Title => "Gameplay";

        public override void Build(VisualElement content)
        {
            var g = Pack.gameplay;
            var modes = PuzzleModeRegistry.Ids.OrderBy(m => m).ToList();
            var names = modes.Select(Fields.Nicify).ToList();

            content.Add(Fields.Section("Puzzle",
                Fields.Dropdown("Mode", names, modes.IndexOf(g.defaultMode), i => { if (i >= 0) { g.defaultMode = modes[i]; Changed(); } }),
                Fields.Hint("Swap Tiles: swap two tiles. Strips: drag strips into order (the others shift). " +
                            "Sliding: the classic sliding puzzle with one gap (max 6 × 6). Rotate: tiles are in place but turned — click to rotate. " +
                            "Memory: every tile is a hidden card — find the pairs to uncover the picture. " +
                            "Each level can use another mode (Levels tab)."),
                Fields.Toggle("Lock correct tiles", g.lockCorrectPieces, v => { g.lockCorrectPieces = v; Changed(); }),
                Fields.FloatSlider("Min. shuffled", 0.3f, 1f, g.minMisplacedRatio, v => { g.minMisplacedRatio = v; Changed(); })));

            var fixedGrid = Fields.IntSlider("Grid size", 2, GridResolver.MaxGrid, g.fixedGrid, v => { g.fixedGrid = v; Changed(); });
            var minGrid = Fields.IntSlider("First level", 2, GridResolver.MaxGrid, g.minGrid, null);
            var maxGrid = Fields.IntSlider("Last level", 2, GridResolver.MaxGrid, g.maxGrid, null);
            minGrid.RegisterValueChangedCallback(e =>
            {
                g.minGrid = e.newValue;
                if (g.maxGrid < g.minGrid) { g.maxGrid = g.minGrid; maxGrid.SetValueWithoutNotify(g.maxGrid); }
                Changed();
            });
            maxGrid.RegisterValueChangedCallback(e =>
            {
                g.maxGrid = e.newValue;
                if (g.minGrid > g.maxGrid) { g.minGrid = g.maxGrid; minGrid.SetValueWithoutNotify(g.minGrid); }
                Changed();
            });

            void UpdateVisibility()
            {
                bool progressive = g.difficultyCurve == DifficultyCurve.Progressive;
                minGrid.style.display = maxGrid.style.display = progressive ? DisplayStyle.Flex : DisplayStyle.None;
                fixedGrid.style.display = progressive ? DisplayStyle.None : DisplayStyle.Flex;
            }

            content.Add(Fields.Section("Difficulty",
                Fields.Enum("Curve", g.difficultyCurve, v => { g.difficultyCurve = v; UpdateVisibility(); Changed(); }),
                minGrid, maxGrid, fixedGrid,
                Fields.Hint("Size N means about N × N tiles; wide or tall pictures get more columns or rows. Progressive grows from the first to the last level. Custom uses each level's own grid (Levels tab).")));
            UpdateVisibility();

            var strips = g.stripsCount;
            content.Add(Fields.Section("Strips mode",
                Fields.Enum("Direction", g.stripsOrientation, v => { g.stripsOrientation = v; Changed(); }),
                Fields.IntSlider("Strips: first level", 2, 24, strips.min, v => { strips.min = v; if (strips.max < v) strips.max = v; Changed(rebuildInspector: true); }),
                Fields.IntSlider("Strips: last level", 2, 24, strips.max, v => { strips.max = v; if (strips.min > v) strips.min = v; Changed(rebuildInspector: true); }),
                Fields.Hint("Vertical = columns to reorder, Horizontal = rows. With a Fixed curve the first value is used.")));

            content.Add(Fields.Section("Memory mode",
                Fields.Enum("Card symbols", g.memorySymbols, v => { g.memorySymbols = v; Changed(); }),
                Fields.Hint("A turned card shows its own piece of the picture and its symbol; the two cards with the same symbol make a pair. " +
                            "Numbers: up to 50 pairs. Letters: A to Z (26 pairs). Colors: a colored outline (12 pairs; players with the " +
                            "colorblind option also see numbers). Bigger grids are reduced to fit; an odd grid gets one free card in the middle. " +
                            "Each level can choose its own symbols (Levels tab).")));

            content.Add(Fields.Section("Progression",
                Fields.Enum("Unlock levels", g.unlockRule, v => { g.unlockRule = v; Changed(); }),
                Fields.IntSlider("Stars to unlock", 0, 3, g.starsToUnlockPerLevel, v => { g.starsToUnlockPerLevel = v; Changed(); }),
                Fields.Hint("Sequential: finish a level to open the next one. All unlocked: everything is open. By stars: each level needs (its number − 1) × \"stars to unlock\" stars in total.")));

            content.Add(Fields.Section("Player help",
                Fields.Toggle("Show timer", g.showTimer, v => { g.showTimer = v; Changed(); }),
                Fields.Toggle("Show moves", g.showMoves, v => { g.showMoves = v; Changed(); }),
                Fields.Toggle("Allow preview", g.allowPreview, v => { g.allowPreview = v; Changed(); }),
                Fields.Toggle("Allow hints", g.allowHints, v => { g.allowHints = v; Changed(); }),
                Fields.IntSlider("Hints per level", 0, 10, g.maxHintsPerLevel, v => { g.maxHintsPerLevel = v; Changed(); }),
                Fields.Toggle("Allow undo", g.allowUndo, v => { g.allowUndo = v; Changed(); })));

            var r = g.starRules;
            content.Add(Fields.Section("Stars",
                Fields.FloatSlider("3 stars up to par ×", 1f, 3f, r.threeStarsMoveFactor, v => { r.threeStarsMoveFactor = v; Changed(); }),
                Fields.FloatSlider("2 stars up to par ×", 1f, 5f, r.twoStarsMoveFactor, v => { r.twoStarsMoveFactor = v; Changed(); }),
                Fields.Toggle("Hint costs a star", r.hintCostsStar, v => { r.hintCostsStar = v; Changed(); }),
                Fields.Hint("Par = the smallest possible number of moves for the shuffled board (Memory: about 1.6 tries per pair, what a perfect memory needs).")));
        }
    }
}
