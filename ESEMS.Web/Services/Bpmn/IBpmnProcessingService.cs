namespace ESEMS.Web.Services.Bpmn;

public interface IBpmnProcessingService
{
    string CleanBpmnXml(string raw);
    bool LooksLikeBpmnXml(string text);

    /// <summary>
    /// Heavy layout pass for the "Enhance Drawing" feature: runs
    /// <see cref="CleanBpmnXml"/> first (diagonal/RTL/color repairs),
    /// then re-routes every sequence/message flow with orthogonal
    /// waypoints anchored to shape boundaries (not centres, so the
    /// edge doesn't tunnel through the shape), centres edge labels on
    /// the new midpoint, and resizes tasks whose label width clearly
    /// overflows the current shape geometry. Idempotent — running it
    /// twice on the same XML produces the same output.
    /// </summary>
    string EnhanceBpmnLayout(string xml);

    /// <summary>
    /// Transposes a horizontal (left-to-right / right-to-left) BPMN diagram
    /// into a vertical (top-to-bottom) flow by reflecting the diagram-
    /// interchange geometry across the main diagonal: swaps x/y on every shape,
    /// label and waypoint, swaps width/height + flips isHorizontal on
    /// pools/lanes, and (for RTL diagrams that would otherwise flow upward)
    /// mirrors Y so the start event stays on top. Returns the input unchanged
    /// on any parse error. Backs the AI generator's "Vertical layout" option.
    /// </summary>
    string MakeBpmnVertical(string xml);
}
