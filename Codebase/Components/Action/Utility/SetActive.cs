using Zios;
using UnityEngine;
namespace Zios{
    public enum ToggleState{Enable,Disable,Toggle}
    [AddComponentMenu("Zios/Component/Action/Set Active")]
    public class SetActive : StateMonoBehaviour{
	    public AttributeGameObject target = new AttributeGameObject();
	    public ToggleState state;
		public AttributeBool revertOnEnd = false;
	    public override void Awake(){
		    base.Awake();
		    this.target.Setup("Target",this);
			this.revertOnEnd.Setup("Revert On End",this);
	    }
		public void Perform(bool flip=false){
			bool state = this.state == ToggleState.Enable ? true : false;
			if(flip){state = !state;}
			foreach(GameObject target in this.target){
				if(target.IsNull()){continue;}
				if(this.state == ToggleState.Toggle){state = !target.activeSelf;}
				if(state != target.activeSelf){target.SetActive(state);}
			}
		}
	    public override void Use(){
			this.Perform();
		    if(this.gameObject.activeSelf){
			    base.Use();
		    }
	    }
		public override void End(){
			if(this.revertOnEnd.Get()){
				this.Perform(true);
			}
			base.End();
		}
    }
}