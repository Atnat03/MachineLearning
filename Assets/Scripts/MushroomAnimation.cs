using System;
using System.Collections;
using Unity.VisualScripting;
using UnityEngine;

public class MushroomAnimation : MonoBehaviour
{
	#region Variables

	[SerializeField] private float _speedRotation = 0.02f;

	#endregion


	#region Fonctions

	private void Update()
	{
		transform.Rotate(Vector3.up * _speedRotation);
	}

	public void Use()
	{
		StartCoroutine(UseAnimation());
	}

	IEnumerator UseAnimation()
	{
		float elapsed = 0f;

		while (elapsed < 0.5f)
		{
			elapsed += Time.deltaTime;
			
			transform.localScale = Vector3.Lerp(transform.localScale, Vector3.zero, elapsed / 0.5f);
			
			yield return null;
		}
		
		Destroy(gameObject);
	}

	#endregion
}
