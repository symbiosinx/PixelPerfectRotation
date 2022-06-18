using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Put this script on a child of the object you want to affect
[RequireComponent(typeof(SpriteRenderer))]
[ExecuteInEditMode]
public class PixelRotation : MonoBehaviour {

    [SerializeField, Range(1, 100)] int pixelsPerUnit = 16;
	[SerializeField, Range(0, 5)] int iterations;
	[SerializeField] bool downscale = true;
    [SerializeField] bool snapAngle = false;
    [SerializeField] int snapDirections = 32;
	[SerializeField] bool rotateNormals = false;
	[SerializeField] bool snapToGrid = true;

    ComputeShader compute;
	ComputeShader cropCompute;
    SpriteRenderer spriteRenderer;
    SpriteRenderer parentSpriteRenderer;

	float prevAngle = float.MaxValue;
    Sprite prevSprite = null;

    void Awake() {
        spriteRenderer = GetComponent<SpriteRenderer>();
        parentSpriteRenderer = transform.parent.GetComponent<SpriteRenderer>();
        compute = Resources.Load<ComputeShader>("ComputeShaders/Scale2xCompute");
		cropCompute = Resources.Load<ComputeShader>("ComputeShaders/SpriteCropCompute");
    }

    void Start() {}

    void LateUpdate() {

        if (!spriteRenderer) spriteRenderer = GetComponent<SpriteRenderer>();
        if (!parentSpriteRenderer) parentSpriteRenderer = transform.parent.GetComponent<SpriteRenderer>();
        if (!compute) compute = Resources.Load<ComputeShader>("ComputeShaders/Scale2xCompute");
		if (!cropCompute) cropCompute = Resources.Load<ComputeShader>("ComputeShaders/SpriteCropCompute");

		spriteRenderer.sortingOrder = parentSpriteRenderer.sortingOrder;
		spriteRenderer.sharedMaterial = parentSpriteRenderer.sharedMaterial;
		spriteRenderer.flipX = parentSpriteRenderer.flipX;
		spriteRenderer.flipY = parentSpriteRenderer.flipY;
		parentSpriteRenderer.enabled = false;

        float angle = transform.parent.eulerAngles.z;
        if (snapAngle) angle = Mathf.RoundToInt(angle/360f*snapDirections)/(float)snapDirections*360f;

        if (angle != prevAngle || parentSpriteRenderer.sprite != prevSprite) {
            if (spriteRenderer && spriteRenderer.sprite && spriteRenderer.sprite.texture)
				DestroyImmediate(spriteRenderer.sprite.texture);
            
			if (parentSpriteRenderer.sprite.texture != null)
            	spriteRenderer.sprite = GenerateSprite(parentSpriteRenderer.sprite, angle);

			if (rotateNormals) {
				Texture2D normalMap = (Texture2D)parentSpriteRenderer.material.GetTexture("_NormalMap");
				Sprite normalSprite = Sprite.Create(normalMap, new Rect(0, 0, normalMap.width, normalMap.height), new Vector2(0.5f, 0.5f), pixelsPerUnit);
				normalSprite = GenerateSprite(normalSprite, angle);
				spriteRenderer.material.SetTexture("_NormalMap", normalSprite.texture);
				spriteRenderer.material.SetVector("_Pos", new Vector4(transform.position.x, transform.position.y, 0f, 0f));
				spriteRenderer.material.SetFloat("_Angle", angle*Mathf.Deg2Rad);
			}
        }

        prevAngle = angle;
        prevSprite = parentSpriteRenderer.sprite;

        Vector3 eulerAngles = transform.eulerAngles;
        eulerAngles.z = 0f;
        transform.eulerAngles = eulerAngles;

		if (snapToGrid) {
			Vector3 pos = transform.position;
			Vector2 newPos = transform.parent.position.xy().SnapToPixel(128);
			pos.x = newPos.x;
			pos.y = newPos.y;
			transform.position = pos;
		}
    }

    void OnValidate() {
		#if UNITY_EDITOR

        float angle = transform.parent.eulerAngles.z;
        if (spriteRenderer && spriteRenderer.sprite && spriteRenderer.sprite.texture)
			DestroyImmediate(spriteRenderer.sprite.texture);

		if (!spriteRenderer) spriteRenderer = GetComponent<SpriteRenderer>();
        if (!parentSpriteRenderer) parentSpriteRenderer = transform.parent.GetComponent<SpriteRenderer>();
        if (!compute) compute = Resources.Load<ComputeShader>("ComputeShaders/Scale2xCompute");
		if (!cropCompute) cropCompute = Resources.Load<ComputeShader>("ComputeShaders/SpriteCropCompute");

		spriteRenderer.sortingOrder = parentSpriteRenderer.sortingOrder;
		spriteRenderer.sharedMaterial = parentSpriteRenderer.sharedMaterial;
		spriteRenderer.flipX = parentSpriteRenderer.flipX;
		spriteRenderer.flipY = parentSpriteRenderer.flipY;
		parentSpriteRenderer.enabled = false;

        spriteRenderer.sprite = GenerateSprite(parentSpriteRenderer.sprite, angle);

		if (rotateNormals) {
			Texture2D normalMap = (Texture2D)parentSpriteRenderer.material.GetTexture("_NormalMap");
			Sprite normalSprite = Sprite.Create(normalMap, new Rect(0, 0, normalMap.width, normalMap.height), new Vector2(0.5f, 0.5f), pixelsPerUnit);
			normalSprite = GenerateSprite(normalSprite, angle);
			spriteRenderer.material.SetTexture("_NormalMap", normalSprite.texture);
			spriteRenderer.material.SetVector("_Pos", new Vector4(transform.position.x, transform.position.y, 0f, 0f));
			spriteRenderer.material.SetFloat("_Angle", angle*Mathf.Deg2Rad);
		}

		#endif
    }

    Sprite GenerateSprite(Sprite inputSprite, float angle) {
		// inputSprite = spriteRenderer.sprite;
		if (inputSprite != null) {

            Vector4 bounds = new Vector4(inputSprite.rect.xMin, inputSprite.rect.yMin, inputSprite.rect.xMax, inputSprite.rect.yMax);
			Texture2D tex = Crop(inputSprite.texture, bounds);

			for (int i = 0; i < iterations; i++)
				tex = UpscaleTexture(tex);

			tex = Rotate(tex, inputSprite.pivot * Mathf.Pow(2, iterations), angle);

			if (downscale) {
				for (int i = 0; i < iterations; i++) {
					tex = DownscaleTexture(tex);
				}
			}

			Sprite sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), pixelsPerUnit * Mathf.Pow(2, iterations*(1f-downscale.ToInt())));
			return sprite;
		}
        return null;

	}

    Texture2D UpscaleTexture(Texture2D input) {
		int kernel = compute.FindKernel("Upscale");
		RenderTexture output = RenderTexture.GetTemporary(input.width*2, input.height*2, 24, RenderTextureFormat.ARGB32);
		output.enableRandomWrite = true;
		output.Create(); 

		// compute.SetVector("inputdimensions", new Vector4(input.width, input.height, 0f, 0f));
		compute.SetTexture(kernel, "input", input);
		compute.SetTexture(kernel, "output", output);
		compute.Dispatch(kernel, output.width/8, output.height/8, 1);

		Texture2D tex = output.ToTexture2D();
		tex.filterMode = FilterMode.Point;

		RenderTexture.ReleaseTemporary(output);
		return tex;

		// // byte[] bytes = tex.EncodeToPNG();
		// // File.WriteAllBytes(Application.dataPath + "/" + input.name + "_" + "scaled.png", bytes);
		// // Debug.Log("Done");

	}

	Texture2D DownscaleTexture(Texture2D input) {
		int kernel = compute.FindKernel("Downscale");
		RenderTexture output = RenderTexture.GetTemporary(input.width/2, input.height/2, 24, RenderTextureFormat.ARGB32);
		output.enableRandomWrite = true;
		output.Create(); 

		compute.SetTexture(kernel, "input", input);
		compute.SetTexture(kernel, "output", output);
		compute.Dispatch(kernel, output.width/8, output.height/8, 1);

		Texture2D tex = output.ToTexture2D();
		tex.filterMode = FilterMode.Point;

		RenderTexture.ReleaseTemporary(output);
		return tex;
	}

	Texture2D Rotate(Texture2D input, Vector2 pivot, float angle) {

		int kernel = compute.FindKernel("Rotate");
		RenderTexture output = RenderTexture.GetTemporary(input.width, input.height, 24, RenderTextureFormat.ARGB32);
		output.enableRandomWrite = true;
		output.Create();

		// compute.SetVector("inputdimensions", new Vector4(input.width, input.height, 0f, 0f));
		compute.SetVector("pivot", new Vector4(pivot.x, pivot.y, 0, 0));
		compute.SetFloat("angle", Mathf.Deg2Rad * angle);
		compute.SetTexture(kernel, "input", input);
		compute.SetTexture(kernel, "output", output);
		compute.Dispatch(kernel, output.width/8, output.height/8, 1);

		Texture2D tex = output.ToTexture2D();
		tex.filterMode = FilterMode.Point;

		RenderTexture.ReleaseTemporary(output);
		return tex;
	}

    Texture2D Crop(Texture2D input, Vector4 bounds) {
        int kernel = cropCompute.FindKernel("Crop");
		RenderTexture output = RenderTexture.GetTemporary(Mathf.RoundToInt(bounds.z-bounds.x), Mathf.RoundToInt(bounds.w-bounds.y), 24, RenderTextureFormat.ARGB32);
		output.enableRandomWrite = true;
		output.Create();

        cropCompute.SetVector("bounds", bounds);
		cropCompute.SetTexture(kernel, "input", input);
		cropCompute.SetTexture(kernel, "output", output);
		cropCompute.Dispatch(kernel, output.width/8, output.height/8, 1);

		Texture2D tex = output.ToTexture2D();
		tex.filterMode = FilterMode.Point;

		RenderTexture.ReleaseTemporary(output);
		return tex;
    }
}
