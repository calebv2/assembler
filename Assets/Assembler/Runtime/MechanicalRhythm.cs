using System.Linq;
using UnityEngine;

namespace ArrowAssemblerNative {
 // Shared by the native mod and Unity preview; animation never changes inventory or collision.
 public sealed class MechanicalRhythm {
  readonly Transform[] bowl;
  readonly Vector3[] homes;
  readonly Transform gear;
  readonly Quaternion gearHome;
  readonly AudioSource clunk;
  bool received;
  int lastCompletion;
  public int CompletionCues {get; private set;}
  public MechanicalRhythm(GameObject root) {
   bowl=root.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="Hollow receiving bowl" || t.name=="Rolled iron bowl lip" || t.name=="Bowl rivet").ToArray();
   homes=bowl.Select(t=>t.localPosition).ToArray();
   gear=root.transform.Find("Crafting side gear");
   gearHome=gear ? gear.localRotation : Quaternion.identity;
   var sound=root.transform.Find("Assembly completion sound");
   clunk=sound ? sound.GetComponent<AudioSource>() : null;
  }
  public void ObserveCompletion(int sequence) {
   if(received && sequence!=lastCompletion) {
    CompletionCues++;
    if(clunk && clunk.clip) clunk.PlayOneShot(clunk.clip);
   }
   received=true; lastCompletion=sequence;
  }
  public void Pose(bool active,float elapsed) {
   float t=Mathf.Clamp(elapsed,0,3);
   float envelope=active ? Mathf.Clamp01(t/.15f)*Mathf.Clamp01((3-t)/.15f) : 0;
   var offset=new Vector3(Mathf.Sin(t*2*Mathf.PI*9)*.0025f,Mathf.Sin(t*2*Mathf.PI*13)*.0012f,0)*envelope;
   for(int i=0;i<bowl.Length;i++)if(bowl[i]) bowl[i].localPosition=homes[i]+offset;
   // One full turn per cycle, easing to rest at both ends. Idle shares its endpoint.
   if(gear)gear.localRotation=gearHome*Quaternion.AngleAxis(active ? 360*Mathf.SmoothStep(0,1,t/3) : 0,Vector3.right);
  }
 }
}
