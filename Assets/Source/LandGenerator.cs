﻿using UnityEngine;

using System.Runtime.Serialization;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System;

using CEUtilities.Helpers;

public class LandGenerator : MonoBehaviour {

	public GameObject player;
	public GameObject borderBox;
	public GameObject baseTile;
	public GameObject loading;

	private static bool DEBUG = false;

	private static float TILE_SCALE = 1;
	private static float TILE_SIZE = 40;

	private Dictionary<Vector2, bool> world = new Dictionary<Vector2, bool>();
	private Dictionary<Vector2, string> names = new Dictionary<Vector2, string>();
	private Dictionary<Vector2, bool> visited = new Dictionary<Vector2, bool>();

	private Vector2 currentTile;

	private string posX = "0";
	private string posZ = "0";

	// Use this for initialization
	void Start () {
		currentTile = GetInitialPosition (Application.absoluteURL);
		player.transform.position = indexToPosition (currentTile);
		posZ = indexToPosition (currentTile).z.ToString ();
		posX = indexToPosition(currentTile).x.ToString ();
		CreatePlaneAt (currentTile);
	}

	// Get initial position from url querystring
	Vector2 GetInitialPosition(string url) {
		if (url.Length > 0) {
			char[] querySplit = { '=', '&' };
			string[] parts = url.Split (querySplit);
			if (parts.Length >= 4) {
				try {
					int x = int.Parse (parts[1]);
					int y = int.Parse (parts[3]);
					return new Vector2 (x, y);
				} catch {}
			}
		}

		return new Vector2(0, 0);
	}

	// This function creates planes for adjacent enviroment
	void CreateEnvironment(Vector3 position) {
		Vector2 current = GetCurrentPlane(player.transform.position);

		// Update Border Box

		if (current != currentTile) {
			borderBox.transform.position = indexToPosition (current);
			currentTile = current;
		}

		// Exit if we already visited this tile
		if (visited.ContainsKey (current)) return;

		// Expand Area
		visited.Add (current, true);
		CreatePlaneAt (current + new Vector2 (0, 1));
		CreatePlaneAt (current + new Vector2 (0, -1));
		CreatePlaneAt (current + new Vector2 (1, 0));
		CreatePlaneAt (current + new Vector2 (-1, 0));
		CreatePlaneAt (current + new Vector2 (1, 1));
		CreatePlaneAt (current + new Vector2 (-1, -1));
		CreatePlaneAt (current + new Vector2 (1, -1));
		CreatePlaneAt (current + new Vector2 (-1, 1));

		CreatePlaneAt (current + new Vector2 (0, 2));
		CreatePlaneAt (current + new Vector2 (0, -2));
		CreatePlaneAt (current + new Vector2 (2, 0));
		CreatePlaneAt (current + new Vector2 (-2, 0));
		CreatePlaneAt (current + new Vector2 (2, 2));
		CreatePlaneAt (current + new Vector2 (-2, -2));
		CreatePlaneAt (current + new Vector2 (2, -2));
		CreatePlaneAt (current + new Vector2 (-2, 2));

		CreatePlaneAt (current + new Vector2 (1, 2));
		CreatePlaneAt (current + new Vector2 (-1, 2));
		CreatePlaneAt (current + new Vector2 (1, -2));
		CreatePlaneAt (current + new Vector2 (-1, -2));
		CreatePlaneAt (current + new Vector2 (2, 1));
		CreatePlaneAt (current + new Vector2 (2, -1));
		CreatePlaneAt (current + new Vector2 (-2, 1));
		CreatePlaneAt (current + new Vector2 (-2, -1));
		CreatePlaneAt (current + new Vector2 (2, 2));
		CreatePlaneAt (current + new Vector2 (-2, -2));
		CreatePlaneAt (current + new Vector2 (2, -2));
		CreatePlaneAt (current + new Vector2 (-2, 2));
	}

	void CreatePlaneAt(Vector2 index) {
		if (world.ContainsKey (index)) return;
		StartCoroutine("FetchTile", index);
		world.Add (index, true);
	}

	// Plane dimentions are TILE_SIZE x TILE_SIZE, with center in the middel.
	Vector2 GetCurrentPlane(Vector3 position) {
		int x = Mathf.CeilToInt ((position[0] - (TILE_SIZE/2)) / TILE_SIZE);
		int z = Mathf.CeilToInt ((position[2] - (TILE_SIZE/2)) / TILE_SIZE);
		return new Vector2(x, z);
	}

	// Update is called once per frame
	void Update () {
		CreateEnvironment (player.transform.position);
	}

	void OnGUI () {
		string tileName;
		if (! names.TryGetValue (currentTile, out tileName)) tileName = "Empty Land";
		string message = tileName + " (" + currentTile [0] + ":" + currentTile [1] + ")";
		GUI.Label (new Rect (10, 10, 200, 20), message);
	}

	private Vector3 indexToPosition(Vector2 index) {
		float x = (index [0] * TILE_SIZE);
		float z = (index [1] * TILE_SIZE);
		return new Vector3 (x, 0, z);
	}

	IEnumerator FetchTile(Vector2 index) {
		string fileName = index[0] + "." + index[1] + ".lnd";
		string host = DEBUG ? "http://lvh.me/tiles" : "https://decentraland.org/content";
		string url = host +  "/" + fileName;

		Vector3 pos = indexToPosition(index);

		// Temporal Placeholder
		GameObject plane = Instantiate(baseTile, pos, Quaternion.identity);
		GameObject loader = Instantiate(loading, pos, Quaternion.identity);
		loader.transform.position = new Vector3(pos.x, pos.y + 2, pos.z);

		WWW www = new WWW(url);
		yield return www;

		if (! string.IsNullOrEmpty(www.error)) {
			Debug.Log("Can't fetch tile content! " + index + " " + www.error);
			names.Add(index, "Unclaimed Land");
			Destroy(loader);
		}
		else {
			Debug.Log("Downloaded content for tile (" + index[0] + "," + index[1] + ")");

			try {
				// TODO: fix decompression effort;
				// byte[] blob = CLZF2.Decompress(www.bytes);
				//
				// Debug.Log("Data length:");
				// Debug.Log(www.bytes.Length);
				// Debug.Log("Decompressed length:");
				// Debug.Log(blob.Length);

				var bundle = AssetBundleUtil.GetBundleFromBytes(www.bytes);
				var prefab = AssetBundleUtil.LoadAssetBundle<GameObject>(bundle);
				var go = Instantiate<GameObject>(prefab);
				bundle.Unload(false);
				Destroy(prefab);

				go.transform.position = pos;
				names.Add(index, go.name);
			} catch (EndOfStreamException e) {
				Debug.Log("Invalid" + index + e.ToString());
			} catch (SerializationException e) {
				Debug.Log("Invalid" + index + e.ToString());
			} catch (Exception e) {
				Debug.Log("Exception found in " + index + e.ToString());
			} finally {
				Destroy(loader);
			}
		}
	}
}

[System.Serializable]
public class RPCError {
	public string message;
	public int code;
}

[System.Serializable]
public class RPCResponse {
	public string result = null;
	public RPCError error = null;
	public string id = null;

	public bool IsUnmined() {
		return this.result == "";
	}

	public bool IsEmpty() {
		// An IPFS hash of all zeroes.
		return this.result == "00000000000000000000000000000000000000000000000000000000000000000000";
	}

	public bool HasData() {
		return ! (this.IsEmpty () || this.IsUnmined ());
	}
}

[System.Serializable]
public class APIResponse {
	public int x;
	public int y;
}
