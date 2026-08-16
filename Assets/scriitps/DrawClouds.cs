using UnityEngine;

#nullable disable
public class GenerateClouds : MonoBehaviour
{
  public ParticleSystem cloud;
  private int n = 1000;

  private void Start() => this.MakeClouds();

  private void MakeClouds()
  {
    for (int index = 0; index < this.n; ++index)
    {
      Vector3 position = this.transform.position + Vector3.right * (float) Random.Range(-250, 250) + Vector3.forward * (float) Random.Range(-250, 250) + Vector3.up * (float) Random.Range(-10, 10);
      Vector3 vector3 = this.transform.localScale * Random.Range(0.75f, 1.5f);
      Quaternion rotation = Quaternion.Euler((float) Random.Range(0, 360), (float) Random.Range(0, 360), (float) Random.Range(0, 360));
      Object.Instantiate<ParticleSystem>(this.cloud, position, rotation).transform.localScale = vector3;
    }
  }
}