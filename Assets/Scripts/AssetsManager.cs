using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

public class AssetsManager {
	public static AssetsManager Instance { get; } = new AssetsManager();

	private static string AssetsUrlPathBase = "https://pulni.com/war";
	private static string AssetsFolder = Application.persistentDataPath + "/Assets";
	private static string AssetsInfoFilePath = AssetsFolder + "/info.json";
	private static string LastAssetsUpdateKey = "LastAssetsUpdate";
	private static string ThumbnailBundleSuffix = "thumb";

	public AssetsInfo assetsInfo;

	private CoroutineHelper coroutineHelper;

	private AssetBundle cardFacesBundle;
	private Dictionary<string, Sprite> cardFaces;

	private AssetBundle backgroundBundle;
	private Sprite backgroundSprite;

	private Sprite cardBackSprite;

	private HashSet<string> thumbnailsInLoading = new HashSet<string>();
	private Dictionary<string, Sprite> loadedThumbnails = new Dictionary<string, Sprite>();

	private Dictionary<string, AssetBundle> loadedBundles = new Dictionary<string, AssetBundle>();

	private AssetsManager() {
		this.LoadAssetsData();

		var go = new GameObject();
		GameObject.DontDestroyOnLoad(go);
		this.coroutineHelper = go.AddComponent<CoroutineHelper>();
	}

	private void LoadAssetsData() {
		try {
			if (File.Exists(AssetsInfoFilePath)) {
				var json = File.ReadAllText(AssetsInfoFilePath);
				this.assetsInfo = JsonUtility.FromJson<AssetsInfo>(json);
			}
		} catch (Exception ex) {
			Debug.LogException(ex);
		}

		if (this.assetsInfo == null) {
			var asset = Resources.Load<TextAsset>("DefaultAssetsInfo");
			this.assetsInfo = JsonUtility.FromJson<AssetsInfo>(asset.text);
		}
	}

	// It's better to be async then a coroutine
	public IEnumerator UpdateAssetsInfo() {
		var now = DateTime.Now;
		var currentDate = now.Year * 1000 + now.DayOfYear;
		var lastUpdate = PlayerPrefs.GetInt(LastAssetsUpdateKey, 0);

		// This is a check so that we only update once a day
		//if (currentDate <= lastUpdate) {
		//	yield break;
		//}

		if (Directory.Exists(AssetsFolder) == false) {
			Directory.CreateDirectory(AssetsFolder);
		}

		var url = "https://pulni.com/war/AssetsInfo.json";
		using (var www = UnityWebRequest.Get(url)) {
			yield return www.SendWebRequest();
			if (www.isNetworkError || www.isHttpError) {
				Debug.Log(www.error);
				yield break;
			} else {
				var json = www.downloadHandler.text;
				this.assetsInfo = JsonUtility.FromJson<AssetsInfo>(json);
				File.WriteAllText(AssetsInfoFilePath, json);
			}
		}

		PlayerPrefs.SetInt(LastAssetsUpdateKey, currentDate);
	}

	public IEnumerator GetThumbnail(string bundleName, bool addThumbnailSuffix, RefData<Sprite> result) {
		yield return this.coroutineHelper.StartCoroutine(this.GetThumbnailInternal(bundleName, addThumbnailSuffix, result));
	}

	private IEnumerator GetThumbnailInternal(string bundleName, bool addThumbnailSuffix, RefData<Sprite> result) {
		var thumbnailBundleName = bundleName;
		if (addThumbnailSuffix) {
			thumbnailBundleName += ThumbnailBundleSuffix;
		}

		// If already loaded - return it quickly
		if (this.loadedThumbnails.TryGetValue(thumbnailBundleName, out var sprite)) {
			result.data = sprite;
			yield break;
		}

		// If it's already been requested - wait for the original request to finish
		if (thumbnailsInLoading.Contains(thumbnailBundleName)) {
			while (thumbnailsInLoading.Contains(thumbnailBundleName)) {
				yield return null;
			}
			result.data = loadedThumbnails[thumbnailBundleName];
			yield break;
		}

		// If it's not loaded and not requested - start loading now
		thumbnailsInLoading.Add(thumbnailBundleName);

		var bundleResult = new RefData<AssetBundle>();
		yield return this.coroutineHelper.StartCoroutine(this.LoadAssetBundle(thumbnailBundleName, bundleResult));

		var assetRequest = bundleResult.data.LoadAllAssetsAsync<Sprite>();
		yield return assetRequest;

		// For testing
		//yield return new WaitForSeconds(UnityEngine.Random.Range(1f, 5f));

		result.data = (Sprite)assetRequest.allAssets[0];
		loadedThumbnails[thumbnailBundleName] = result.data;
		thumbnailsInLoading.Remove(thumbnailBundleName);
	}

	public IEnumerator LoadCardsPack(string cardsFacesPack) {
		if (this.cardFacesBundle != null && this.cardFacesBundle.name == cardsFacesPack) {
			yield break;
		}

		var result = new RefData<AssetBundle>();
		yield return this.coroutineHelper.StartCoroutine(this.LoadAssetBundle(cardsFacesPack, result));
		
		var request = result.data.LoadAllAssetsAsync<Sprite>();
		yield return request;

		this.cardFaces = request.allAssets.Cast<Sprite>().ToDictionary(sp => sp.name);

		if (this.cardFacesBundle != null) {
			this.loadedBundles.Remove(this.cardFacesBundle.name);
			this.cardFacesBundle.Unload(true);
		}

		this.cardFacesBundle = result.data;
	}

	public IEnumerator LoadBackground(string backgroundBundle) {
		//if (this.backgroundBundle != null && this.backgroundBundle.name == backgroundBundle) {
		//	yield break;
		//}

		var result = new RefData<AssetBundle>();
		yield return this.coroutineHelper.StartCoroutine(this.LoadAssetBundle(backgroundBundle, result));

		var request = result.data.LoadAllAssetsAsync<Sprite>();
		yield return request;

		this.backgroundSprite = (Sprite)request.allAssets[0];

		//if (this.backgroundBundle != null) {
		//	this.loadedBundles.Remove(this.backgroundBundle.name);
		//	this.backgroundBundle.Unload(true);
		//}

		//this.backgroundBundle = result.data;
	}

	public IEnumerator LoadCardBack(string cardBackBundle) {
		var result = new RefData<AssetBundle>();
		yield return this.coroutineHelper.StartCoroutine(this.LoadAssetBundle(cardBackBundle, result));

		var request = result.data.LoadAllAssetsAsync<Sprite>();
		yield return request;

		this.cardBackSprite = (Sprite)request.allAssets[0];
	}

	private IEnumerator LoadAssetBundle(string bundleName, RefData<AssetBundle> result) {
		if (this.loadedBundles.TryGetValue(bundleName, out var bundle)) {
			result.data = bundle;
			yield break;
		}

		var url = AssetsUrlPathBase + "/" + bundleName;
		using (var request = UnityWebRequestAssetBundle.GetAssetBundle(url, 1, 0)) {
			yield return request.SendWebRequest();
			result.data = DownloadHandlerAssetBundle.GetContent(request);
			this.loadedBundles[bundleName] = result.data;
		}
	}

	public Sprite GetCardSprite(CardSuit suit, CardRank rank) {
		var name = $"{suit}_{(int)rank}";
		return this.cardFaces[name];
	}

	public Sprite GetBackgroundSprite() {
		return this.backgroundSprite;
	}

	public Sprite GetCardBackSprite() {
		return this.cardBackSprite;
	}

	private class CoroutineHelper : MonoBehaviour { }
}
