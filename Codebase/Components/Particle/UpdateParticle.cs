﻿using UnityEngine;
[ExecuteInEditMode]
public class UpdateParticle : MonoBehaviour{
	#if UNITY_EDITOR
	public void OnWillRenderObject(){
		if(!Application.isPlaying && Camera.current != null){
			float range = UnityEditor.EditorPrefs.GetFloat("EditorSettings-ParticleUpdateRange");
			Vector3 cameraPosition = Camera.current.transform.position;
			Vector3 objectPosition = this.transform.position;
			if(Vector3.Distance(cameraPosition,objectPosition) <= range){
				this.GetComponent<ParticleSystem>().Simulate(Time.realtimeSinceStartup%60+10);
			}
		}
	}
	#endif
}
