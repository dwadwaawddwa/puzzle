using PuzzleStudio.Core.Data;
using PuzzleStudio.Studio.App;
using UnityEngine.UIElements;

namespace PuzzleStudio.Studio.Panels
{
    /// <summary>One tab of the Studio's left navigation; draws its settings in the right inspector.</summary>
    public abstract class StudioPanel
    {
        protected readonly StudioApp App;
        protected StudioPanel(StudioApp app) { App = app; }

        protected GamePackData Pack => App.Project.Pack;

        public abstract string Id { get; }
        public abstract string Title { get; }

        /// <summary>Builds the panel content (called when shown and after structural changes).</summary>
        public abstract void Build(VisualElement content);

        /// <summary>Called when the tab becomes active / inactive.</summary>
        public virtual void OnActivated() { }
        public virtual void OnDeactivated() { }

        /// <summary>Records a change: marks the project dirty and refreshes the live preview.</summary>
        protected void Changed(bool rebuildInspector = false) => App.MarkDirty(rebuildInspector);
    }
}
