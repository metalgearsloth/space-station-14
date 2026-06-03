using Robust.Client.Graphics;
using Robust.Shared.Maths;
using Content.Shared.Fluids.Components;

namespace Content.Client.Fluids.Components;

[RegisterComponent]
public sealed partial class PuddleComponent : SharedPuddleComponent
{
    public ShaderInstance? Shader;

    public Color SolutionColor = Color.White;

    public Color ShaderColor = Color.White;

    public Color NorthShaderColor = Color.White;

    public Color SouthShaderColor = Color.White;

    public Color EastShaderColor = Color.White;

    public Color WestShaderColor = Color.White;

    public EntityUid? GridUid;

    public Vector2i GridIndices;
}
