using Zios;
using System;
using UnityEngine;
[AddComponentMenu("Zios/Component/Action/Attribute/Modify/Modify GameObject")]
public class AttributeModifyGameObject : ActionPart{
	public AttributeGameObject target;
	public AttributeGameObject value;
	public override void Awake(){
		base.Awake();
		this.target.info.mode = AttributeMode.Linked;
		this.target.Setup("Target",this);
		this.value.Setup("Value",this);
	}
	public override void Use(){
		this.target.Set(this.value.Get());
		base.Use();
	}
}
