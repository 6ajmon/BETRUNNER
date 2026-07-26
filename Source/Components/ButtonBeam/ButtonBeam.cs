using Godot;

public partial class ButtonBeam : Node3D
{
    [Export] public Node3D ButtonNode { get; set; }
    [Export] public float BeamRadius { get; set; } = 0.12f;
    [Export] public Color BeamColor { get; set; } = new Color(0.75f, 0.61f, 0.0f, 0.63f);
    [Export] public float VerticalOffset { get; set; } = 0.15f;

    private Node3D _pivot;
    private MeshInstance3D _meshInstance;

    public override void _Ready()
    {
        _pivot = new Node3D();
        AddChild(_pivot);

        _meshInstance = new MeshInstance3D();
        _pivot.AddChild(_meshInstance);

        var cylinderMesh = new CylinderMesh();
        cylinderMesh.TopRadius = BeamRadius;
        cylinderMesh.BottomRadius = BeamRadius;
        _meshInstance.Mesh = cylinderMesh;

        var mat = new StandardMaterial3D();
        mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        mat.BlendMode = BaseMaterial3D.BlendModeEnum.Add;
        mat.AlbedoColor = BeamColor;
        mat.EmissionEnabled = true;
        mat.Emission = BeamColor;
        mat.EmissionEnergyMultiplier = 4.0f;

        _meshInstance.MaterialOverride = mat;

        if (ButtonNode == null)
        {
            GD.PrintErr($"[{Name}] WARNING: Assign ButtonNode in the Inspector!");
        }
    }

    public override void _Process(double delta)
    {
        if (GodotObject.IsInstanceValid(ButtonNode))
        {
            Vector3 offsetVector = new Vector3(0, VerticalOffset, 0);
            Vector3 startPos = ButtonNode.GlobalPosition - offsetVector;
            Vector3 endPos = GlobalPosition - offsetVector;

            Vector3 dir = endPos - startPos;
            float distance = dir.Length();

            if (distance > 0.001f)
            {
                _pivot.GlobalPosition = startPos;

                Vector3 yAxis = dir.Normalized();
                Vector3 xAxis = Mathf.Abs(yAxis.Dot(Vector3.Up)) < 0.99f 
                    ? Vector3.Up.Cross(yAxis).Normalized() 
                    : Vector3.Right.Cross(yAxis).Normalized();
                Vector3 zAxis = xAxis.Cross(yAxis).Normalized();

                _pivot.GlobalBasis = new Basis(xAxis, yAxis, zAxis);
                
                if (_meshInstance.Mesh is CylinderMesh cylinder)
                {
                    cylinder.Height = distance;
                }
                _meshInstance.Position = new Vector3(0, distance * 0.5f, 0);
            }
        }
    }
}