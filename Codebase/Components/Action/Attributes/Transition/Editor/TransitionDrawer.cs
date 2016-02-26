using UnityEditor;
using UnityEngine;
namespace Zios.Editors{
	using Actions.TransitionComponents;
	using Interface;
	using Events;
	[CustomPropertyDrawer(typeof(Transition))]
	public class TransitionDrawer : PropertyDrawer{
		public override float GetPropertyHeight(SerializedProperty property,GUIContent label){
			var hash = property.GetObject<Transition>().path;
			if(EditorPrefs.GetBool(hash)){return EditorGUIUtility.singleLineHeight*5+8;}
			return base.GetPropertyHeight(property,label);
		}
		public override void OnGUI(Rect area,SerializedProperty property,GUIContent label){
			Transition transition = property.GetObject<Transition>();
			var spacing = area.height = EditorGUIUtility.singleLineHeight;
			if(!transition.time.isSetup){return;}
			if("Transition".DrawFoldout(area,transition.path,true)){
				EditorGUI.indentLevel += 1;
				transition.time.Set(transition.time.Get().Draw(area.AddY(spacing+2),"Time",null,true));
				transition.speed.Set(transition.speed.Get().Draw(area.AddY(spacing*2+4),"Speed",null,true));
				transition.acceleration = transition.acceleration.Draw(area.AddY(spacing*3+6),"Acceleration",true);
				transition.deceleration = transition.deceleration.Draw(area.AddY(spacing*4+8),"Deceleration",true);
				EditorGUI.indentLevel -= 1;
			}
			if(GUI.changed){
				property.serializedObject.targetObject.CallEvent("On Validate");
				transition.Setup(transition.path,transition.parent);
			}
		}
	}
}