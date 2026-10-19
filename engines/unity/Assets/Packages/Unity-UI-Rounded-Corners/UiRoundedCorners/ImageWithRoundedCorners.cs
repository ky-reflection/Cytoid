using System;
using UnityEngine;
using UnityEngine.UI;

[ExecuteInEditMode]
[UnityEngine.Scripting.Preserve]
public class ImageWithRoundedCorners : MonoBehaviour {
	private static readonly int Props = Shader.PropertyToID("_WidthHeightRadius");
	private static readonly int BorderWidthProp = Shader.PropertyToID("_BorderWidth");

	public Material material;
	public float radius;
	public float borderWidth;

	void OnRectTransformDimensionsChange(){
		Refresh();
	}

	private void OnValidate(){
		Refresh();
	}

	private void Refresh(){
		// AddComponent invokes OnValidate before the caller assigns the material.
		if (material == null || !(transform is RectTransform rectTransform)) return;
		var rect = rectTransform.rect;
		material.SetVector(Props, new Vector4(rect.width, rect.height, radius, 0));
		material.SetFloat(BorderWidthProp, borderWidth);
	}
}
