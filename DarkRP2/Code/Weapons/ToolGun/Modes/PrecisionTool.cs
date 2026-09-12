/// <summary>
/// Precision tool mode for exact prop placement (Easy Precision style).
/// </summary>
public enum PrecisionMode
{
	Apply,
	Move,
	Rotate,
	Weld
}

[Icon( "📐" )]
[Title( "#tool.name.precision" )]
[ClassName( "precision" )]
[Group( "#tool.group.building" )]
public class PrecisionTool : ToolMode
{
	const float MaxNudge = 100f;
	const float MaxOffset = 256f;

	[Property, Sync]
	public PrecisionMode Mode { get; set; } = PrecisionMode.Move;

	[Property, Sync, Range( 0.1f, MaxNudge ), Step( 0.5f ), Title( "Nudge" )]
	public float Nudge { get; set; } = 25f;

	[Property, Sync, Title( "Nudge %" )]
	public bool NudgePercent { get; set; }

	[Property, Sync, Range( -MaxOffset, MaxOffset ), Step( 0.5f ), Title( "Offset" )]
	public float Offset { get; set; }

	[Property, Sync, Title( "Offset %" )]
	public bool OffsetPercent { get; set; }

	[Property, Sync]
	public bool Freeze { get; set; } = true;

	[Property, Sync, Title( "Auto Rotate" )]
	public bool AutoRotate { get; set; }

	[Property, Sync, Title( "Rotate After Move" )]
	public bool RotateAfterMove { get; set; } = true;

	[Property, Sync, Range( 0, 90 ), Step( 1 ), Title( "Rotation Snap" )]
	public float RotationSnap { get; set; } = 15f;

	SelectionPoint _point1;
	SelectionPoint _point2;
	int _stage;
	float _easyModeAngle;
	float _rawAngle;
	bool _isSnapping;
	Vector3 _rotateAxis = Vector3.Up;

	public override bool UseSnapGrid => Mode is PrecisionMode.Move or PrecisionMode.Weld;

	public override bool AbsorbMouseInput => _stage == 2 || (Mode == PrecisionMode.Rotate && _stage == 1);

	public override string Description => Mode switch
	{
		PrecisionMode.Apply => "#tool.hint.precision.apply.description",
		PrecisionMode.Rotate => _stage == 1
			? "#tool.hint.precision.rotate.stage1"
			: "#tool.hint.precision.rotate.description",
		PrecisionMode.Weld => _stage switch
		{
			2 => "#tool.hint.precision.rotate.stage1",
			1 => "#tool.hint.precision.move.stage1",
			_ => "#tool.hint.precision.weld.description"
		},
		_ => _stage switch
		{
			2 => "#tool.hint.precision.rotate.stage1",
			1 => "#tool.hint.precision.move.stage1",
			_ => "#tool.hint.precision.move.description"
		}
	};

	public override string PrimaryAction => Mode switch
	{
		PrecisionMode.Apply => "#tool.hint.precision.apply",
		PrecisionMode.Rotate => _stage == 1
			? "#tool.hint.precision.confirm"
			: "#tool.hint.precision.rotate",
		_ => _stage switch
		{
			2 => "#tool.hint.precision.confirm",
			1 => "#tool.hint.precision.finish",
			_ => "#tool.hint.precision.source"
		}
	};

	public override string SecondaryAction => (_stage == 2 || (Mode == PrecisionMode.Rotate && _stage == 1))
		? "#tool.hint.precision.cancel"
		: "#tool.hint.precision.push";

	public override string ReloadAction => (_stage == 2 || (Mode == PrecisionMode.Rotate && _stage == 1))
		? null
		: "#tool.hint.precision.pull";

	protected override void OnDisabled()
	{
		base.OnDisabled();
		ResetState();
	}

	void ResetState()
	{
		_stage = 0;
		_point1 = default;
		_point2 = default;
		ResetRotation();
	}

	void ResetRotation()
	{
		_easyModeAngle = 0f;
		_rawAngle = 0f;
		_isSnapping = false;
	}

	public override void OnControl()
	{
		Toolgun.SetIsUsingJoystick( AbsorbMouseInput );

		if ( Mode == PrecisionMode.Rotate && _stage == 1 )
		{
			HandleStandaloneRotate();
			return;
		}

		if ( _stage == 2 && Mode is PrecisionMode.Move or PrecisionMode.Weld )
		{
			SnapGrid?.Hide();
			HandleMoveRotateStage();
			return;
		}

		base.OnControl();

		var select = TraceSelect();
		var isPlacementMode = Mode is PrecisionMode.Move or PrecisionMode.Weld;
		IsValidState = IsValidTarget( select, allowWorld: isPlacementMode && _stage == 1 );

		if ( isPlacementMode && _stage == 1 && IsValidState && _point1.IsValid() )
		{
			var preview = Input.Down( "use" ) ? SnapSelectionPoint( select ) : select;
			if ( preview.IsValid() && preview.GameObject != _point1.GameObject )
				DrawPreview( GetOffsetPlacement( _point1, preview ) );
		}

		if ( !IsValidState )
			return;

		if ( Input.Pressed( "attack2" ) )
		{
			if ( _stage != 0 )
			{
				ResetState();
				return;
			}

			if ( !FireToolAction( ToolInput.Secondary ) )
				return;

			NudgeObject( select, -1 );
			ShootEffects( select );
			FirePostToolAction( ToolInput.Secondary );
			return;
		}

		if ( Input.Pressed( "reload" ) )
		{
			if ( _stage != 0 )
			{
				ResetState();
				return;
			}

			if ( !FireToolAction( ToolInput.Reload ) )
				return;

			NudgeObject( select, +1 );
			ShootEffects( select );
			FirePostToolAction( ToolInput.Reload );
			return;
		}

		if ( !Input.Pressed( "attack1" ) )
			return;

		switch ( Mode )
		{
			case PrecisionMode.Apply:
				OnApply( select );
				break;
			case PrecisionMode.Rotate:
				OnBeginRotate( select );
				break;
			case PrecisionMode.Move:
			case PrecisionMode.Weld:
				OnMovePrimary( select );
				break;
		}
	}

	bool IsValidTarget( SelectionPoint select, bool allowWorld = false )
	{
		if ( !select.IsValid() ) return false;
		if ( select.IsPlayer ) return false;
		if ( select.IsWorld && !allowWorld ) return false;
		return true;
	}

	static GameObject ResolveRoot( GameObject go )
	{
		return go?.Network?.RootGameObject ?? go;
	}

	SelectionPoint SnapSelectionPoint( SelectionPoint select )
	{
		if ( !select.IsValid() || SnapGrid == null ) return select;

		var snapPos = SnapGrid.LastSnapWorldPos;
		var snappedLocalPos = select.GameObject.WorldTransform.ToLocal( new Transform( snapPos ) ).Position;
		var lt = select.LocalTransform;
		lt.Position = snappedLocalPos;
		select.LocalTransform = lt;
		return select;
	}

	void OnApply( SelectionPoint select )
	{
		if ( !IsValidTarget( select ) )
			return;

		if ( !FireToolAction( ToolInput.Primary ) )
			return;

		ApplySettings( ResolveRoot( select.GameObject ) );
		ShootEffects( select );
		FirePostToolAction( ToolInput.Primary );
	}

	void OnBeginRotate( SelectionPoint select )
	{
		if ( !IsValidTarget( select ) )
			return;

		_point1 = select;
		_rotateAxis = select.WorldTransform().Rotation.Forward;
		_stage = 1;
		ResetRotation();
		ShootEffects( select );
	}

	void HandleStandaloneRotate()
	{
		if ( !_point1.IsValid() || Input.Pressed( "attack2" ) )
		{
			ResetState();
			return;
		}

		IsValidState = true;
		UpdateRotationInput();

		var root = ResolveRoot( _point1.GameObject );
		var pivot = _point1.WorldPosition();
		var axisRotation = Rotation.FromAxis( _rotateAxis, _easyModeAngle );
		var preview = root.WorldTransform;
		preview.Position = pivot + axisRotation * (preview.Position - pivot);
		preview.Rotation = axisRotation * root.WorldRotation;
		DrawPreview( preview );

		if ( !Input.Pressed( "attack1" ) )
			return;

		if ( !FireToolAction( ToolInput.Primary ) )
		{
			ResetState();
			return;
		}

		ApplyRotation( root, pivot, _rotateAxis, _easyModeAngle );
		ShootEffects( _point1 );
		FirePostToolAction( ToolInput.Primary );
		ResetState();
	}

	void OnMovePrimary( SelectionPoint select )
	{
		if ( _stage == 0 )
		{
			if ( !IsValidTarget( select ) )
				return;

			_point1 = Input.Down( "use" ) ? SnapSelectionPoint( select ) : select;
			_stage = 1;
			ShootEffects( select );
			return;
		}

		if ( _stage != 1 )
			return;

		select = Input.Down( "use" ) ? SnapSelectionPoint( select ) : select;
		if ( !IsValidTarget( select, allowWorld: true ) )
			return;
		if ( !_point1.IsValid() || select.GameObject == _point1.GameObject )
			return;

		_point2 = select;

		if ( RotateAfterMove )
		{
			ResetRotation();
			_stage = 2;
			ShootEffects( select );
			return;
		}

		FinishMove( _point1, _point2, 0f );
	}

	void HandleMoveRotateStage()
	{
		if ( Input.Down( "attack2" ) || !_point1.IsValid() || !_point2.IsValid() )
		{
			ResetState();
			return;
		}

		IsValidState = true;
		UpdateRotationInput();
		DrawPreview( GetRotatedPlacement( _point1, _point2, _easyModeAngle ) );

		if ( !Input.Pressed( "attack1" ) )
			return;

		FinishMove( _point1, _point2, _easyModeAngle );
	}

	void FinishMove( SelectionPoint point1, SelectionPoint point2, float angle )
	{
		if ( !FireToolAction( ToolInput.Primary ) )
		{
			ResetState();
			return;
		}

		if ( Mode == PrecisionMode.Weld )
			CreatePrecisionWeld( point1, point2, angle );
		else
			ApplyMove( point1, point2, angle );

		ShootEffects( point2 );
		FirePostToolAction( ToolInput.Primary );
		ResetState();
	}

	void UpdateRotationInput()
	{
		var snap = MathF.Max( RotationSnap, 0f );
		var isSnapping = Input.Down( "run" ) && snap >= 0.001f;
		if ( !isSnapping && _isSnapping )
			_rawAngle = _easyModeAngle;
		_isSnapping = isSnapping;

		var delta = Input.AnalogLook.yaw;
		_rawAngle += delta;

		_easyModeAngle = _isSnapping
			? MathF.Round( _rawAngle / snap ) * snap
			: _rawAngle;

		Toolgun.UpdateJoystick( new Angles( delta, 0, 0 ) );
	}

	void DrawPreview( Transform placement )
	{
		if ( !_point1.IsValid() )
			return;

		var go = ResolveRoot( _point1.GameObject );
		var builder = new LinkedGameObjectBuilder();
		builder.AddConnected( go );

		foreach ( var obj in builder.Objects )
		{
			var previewTransform = obj == go
				? placement
				: placement.ToWorld( go.WorldTransform.ToLocal( obj.WorldTransform ) );

			DebugOverlay.GameObject( obj, transform: previewTransform, color: Color.White.WithAlpha( 0.3f ) );
		}
	}

	Transform GetOffsetPlacement( SelectionPoint a, SelectionPoint b )
	{
		var placement = GetEasyModePlacement( a, b );
		var offset = ResolveOffset( a );
		if ( MathF.Abs( offset ) > 0.001f )
			placement.Position += b.WorldTransform().Rotation.Forward * offset;
		return placement;
	}

	Transform GetRotatedPlacement( SelectionPoint a, SelectionPoint b, float angle )
	{
		var placement = GetOffsetPlacement( a, b );
		var contactPoint = b.WorldPosition();
		var axisRotation = Rotation.FromAxis( -b.WorldTransform().Rotation.Forward, angle );

		placement.Position = contactPoint + axisRotation * (placement.Position - contactPoint);
		placement.Rotation = axisRotation * placement.Rotation;
		return placement;
	}

	float ResolveOffset( SelectionPoint select )
	{
		var value = MathX.Clamp( Offset, -MaxOffset, MaxOffset );
		if ( !OffsetPercent )
			return value;

		return GetEntityOffsetPercent( value, ResolveRoot( select.GameObject ), select.WorldTransform().Rotation.Forward );
	}

	float ResolveNudgeFor( GameObject go, Vector3 normal )
	{
		var value = MathX.Clamp( Nudge, 0.1f, MaxNudge );
		if ( !NudgePercent )
			return value;

		return GetEntityOffsetPercent( value, go, normal );
	}

	static float GetEntityOffsetPercent( float percent, GameObject go, Vector3 normal )
	{
		if ( !go.IsValid() )
			return percent;

		var size = go.GetBounds().Size;
		var rot = go.WorldRotation;

		if ( MathF.Abs( normal.Dot( rot.Forward ) ) > 0.8f )
			return size.x * percent / 100f;
		if ( MathF.Abs( normal.Dot( rot.Left ) ) > 0.8f )
			return size.y * percent / 100f;

		return size.z * percent / 100f;
	}

	void NudgeObject( SelectionPoint select, int direction )
	{
		if ( !IsValidTarget( select ) )
			return;

		var normal = select.WorldTransform().Rotation.Forward;
		NudgeHost( ResolveRoot( select.GameObject ), normal, direction );
	}

	[Rpc.Host( NetFlags.OwnerOnly )]
	void ApplySettings( GameObject go )
	{
		if ( !go.IsValid() || go.IsProxy )
			return;
		if ( !CanUseToolOn( go ) )
			return;
		if ( !TryUseToolActionCooldown() )
			return;

		if ( AutoRotate )
		{
			var angles = go.WorldRotation.Angles();
			angles = new Angles(
				MathF.Round( angles.pitch / 45f ) * 45f,
				MathF.Round( angles.yaw / 45f ) * 45f,
				MathF.Round( angles.roll / 45f ) * 45f
			);
			go.WorldRotation = angles;
		}

		SetFrozen( go, Freeze );
	}

	[Rpc.Host( NetFlags.OwnerOnly )]
	void NudgeHost( GameObject go, Vector3 normal, int direction )
	{
		if ( !go.IsValid() || go.IsProxy )
			return;
		if ( !CanUseToolOn( go ) )
			return;
		if ( !TryUseToolActionCooldown() )
			return;
		if ( normal.Length < 0.001f )
			return;

		direction = direction >= 0 ? 1 : -1;
		var amount = ResolveNudgeFor( go, normal ) * direction;
		go.WorldPosition += normal.Normal * amount;
		SetFrozen( go, Freeze );
	}

	[Rpc.Host( NetFlags.OwnerOnly )]
	void ApplyRotation( GameObject go, Vector3 pivot, Vector3 axis, float angle )
	{
		if ( !go.IsValid() || go.IsProxy )
			return;
		if ( !CanUseToolOn( go ) )
			return;
		if ( !TryUseToolActionCooldown() )
			return;

		angle = MathX.Clamp( angle, -3600f, 3600f );
		if ( axis.Length < 0.001f )
			return;

		var rotation = Rotation.FromAxis( axis.Normal, angle );
		go.WorldPosition = pivot + rotation * (go.WorldPosition - pivot);
		go.WorldRotation = rotation * go.WorldRotation;
		SetFrozen( go, Freeze );
	}

	[Rpc.Host( NetFlags.OwnerOnly )]
	void ApplyMove( SelectionPoint point1, SelectionPoint point2, float angle )
	{
		if ( !CanUseToolOn( point1 ) )
			return;
		if ( point2.IsValid() && !point2.IsWorld && !CanUseToolOn( point2 ) )
			return;
		if ( !point1.GameObject.IsValid() || !point2.GameObject.IsValid() )
			return;
		if ( point1.GameObject == point2.GameObject )
			return;
		if ( !TryUseToolActionCooldown() )
			return;

		angle = MathX.Clamp( angle, -3600f, 3600f );
		var moving = ResolveRoot( point1.GameObject );
		moving.WorldTransform = GetRotatedPlacement( point1, point2, angle );
		SetFrozen( moving, Freeze );
	}

	[Rpc.Host( NetFlags.OwnerOnly )]
	void CreatePrecisionWeld( SelectionPoint point1, SelectionPoint point2, float angle )
	{
		if ( !CanUseToolOn( point1 ) )
			return;
		if ( point2.IsValid() && !point2.IsWorld && !CanUseToolOn( point2 ) )
			return;
		if ( !point1.GameObject.IsValid() || !point2.GameObject.IsValid() )
			return;
		if ( point1.GameObject == point2.GameObject )
			return;
		if ( !TryUseToolActionCooldown() )
			return;

		angle = MathX.Clamp( angle, -3600f, 3600f );
		var moving = ResolveRoot( point1.GameObject );
		moving.WorldTransform = GetRotatedPlacement( point1, point2, angle );
		SetFrozen( moving, Freeze );

		var go1 = new GameObject( false, "weld" );
		go1.Parent = point1.GameObject;
		go1.LocalTransform = point1.LocalTransform;
		go1.LocalRotation = Rotation.Identity;
		go1.Tags.Add( "constraint" );

		var go2 = new GameObject( false, "weld" );
		go2.Parent = point2.GameObject;
		go2.LocalTransform = point2.LocalTransform;
		go2.LocalRotation = Rotation.Identity;
		go2.Tags.Add( "constraint" );

		var cleanup = go1.AddComponent<ConstraintCleanup>();
		cleanup.Attachment = go2;

		var joint = go1.AddComponent<FixedJoint>();
		joint.Attachment = Joint.AttachmentMode.Auto;
		joint.Body = go2;
		joint.EnableCollision = true;
		joint.AngularFrequency = 10;
		joint.LinearFrequency = 10;

		go2.NetworkSpawn();
		go1.NetworkSpawn();

		Track( go1, go2 );

		var undo = Player.Undo.Create();
		undo.Name = "Precision Weld";
		undo.Icon = "📐";
		undo.Add( go1 );
		undo.Add( go2 );

		CheckContraptionStats( point1.GameObject );
		Player.PlayerData?.AddStat( "tool.weld.create" );
	}

	static void SetFrozen( GameObject go, bool freeze )
	{
		if ( !freeze )
			return;

		var rb = go.GetComponent<Rigidbody>() ?? go.GetComponentInChildren<Rigidbody>();
		if ( rb.IsValid() )
			rb.MotionEnabled = false;
	}
}
