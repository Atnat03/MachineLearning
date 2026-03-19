using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.UI;

public class AgentManager : MonoBehaviour
{
	#region Properties

	#endregion

	#region Variables

	[SerializeField] private int _populationSize = 100;
	[SerializeField] private float _trainingDuration = 30f;
	
	[SerializeField] private Agent _agentPrefab;
	[SerializeField] private Transform _agentGroup;
	[SerializeField] private CameraFollow _camera;
	[SerializeField] private LayerMask _layerMaskOnlyBest;
	[SerializeField] private LayerMask _layerMaskDefault;
	[SerializeField] private AnimationCurve _trainingDurationByFitness;
	
	[Header("Mutations")]
	[SerializeField] private float _mutationRate = 0.5f;
	[SerializeField] private float _mutationPower = 0.5f;

	[Header("UI")] 
	[SerializeField] private TextMeshProUGUI _txtTimer;
	[SerializeField] private TextMeshProUGUI _txtGeneration;
	[SerializeField] private TextMeshProUGUI _sliderTimeText;
	[SerializeField] private Slider _sliderTimeScale;
	[SerializeField] private Toggle _toggleBestAgent;
	
	private Agent _agent;
	private List<Agent> _agentList = new();
	private float elapsedTraningTime = 0f;
	private int _numberGeneration = 0;
	private float timeScale = 1;
	private float maxTimeScale = 5;
	
	#endregion

	#region Fonctions

	void Awake()
	{
		//StartCoroutine(Loop());
		_trainingDuration = 5;

		_sliderTimeScale.value = 0.2f;
	}
	
	private void Update()
	{
		if (elapsedTraningTime > 0)
		{
			elapsedTraningTime -= Time.deltaTime;
		}
		else
		{
			ResetGeneration();
		}
		
		_txtTimer.text = elapsedTraningTime.ToString("F0");
		_txtGeneration.text = "Générations : " + _numberGeneration;
		_sliderTimeText.text = timeScale.ToString("F1");

		timeScale = Mathf.Lerp(0, maxTimeScale, _sliderTimeScale.value);
		
		Time.timeScale = timeScale;
	}
	
	private void StartNewGeneration()
	{
		AddOrRemoveAgent();

		_agentList = _agentList.OrderByDescending(a => a.Fitness).ToList();
		
		_trainingDuration = _trainingDurationByFitness.Evaluate(_agentList[0].Fitness);

		Mutate();
		ResetAgents();
		SetMaterials();
		SetLayers();

		FocusCamera();
		
		_numberGeneration++;
	}

	private void AddOrRemoveAgent()
	{
		if (_agentList.Count != _populationSize)
		{
			int diff =  _populationSize - _agentList.Count;

			if (diff > 0)
			{
				for (int i = 0; i < diff; i++)
				{
					AddAgent();
				}
			}else
			{
				for (int i = 0; i < -diff; i++)
				{
					RemoveAgent();
				}
			}
		}
	}

	private void AddAgent()
	{
		_agent = Instantiate(_agentPrefab, Vector3.zero, Quaternion.identity, _agentGroup);
		_agent.Network = new NeuralNetwork(_agent.Network._layers);
		_agentList.Add(_agent);
	}

	private void RemoveAgent()
	{
		Destroy(_agentList[^1].gameObject);
		_agentList.Remove(_agentList[^1]);
	}
	
	private void Mutate()
	{
		for (int i = _agentList.Count/2; i < _agentList.Count; i++)
		{
			_agentList[i].Network.CopyNet(_agentList[i - _agentList.Count /2].Network);
			_agentList[i].Network.Mutate(_mutationRate,  _mutationPower);
			_agentList[i].SetMaterial(2);
		}
	}
	
	private void ResetAgents()
	{
		foreach (Agent agentToReset in _agentList)
		{
			agentToReset.ResetAgent();
		}
	}
	
	private void SetMaterials()
	{
		for (int i = 1; i < _agentList.Count / 2; i++)
		{
			_agentList[i].SetMaterial(1);
		}
		
		_agentList[0].SetMaterial(0);
	}

	private void SetLayers()
	{
		foreach (Agent agent in _agentList)
		{
			SetLayerRecursively(agent.gameObject, LayerMask.NameToLayer("Car"));
		}
		
		SetLayerRecursively(_agentList[0].gameObject, LayerMask.NameToLayer("BestCar"));
	}

	public void ResetGeneration()
	{
		foreach (Agent agentToReset in _agentList)
		{
			agentToReset.FitnessEndGeneration();
		}
		
		elapsedTraningTime = _trainingDuration;
		StartNewGeneration();
	}

	void FocusCamera()
	{
		NeuralNetworkViewer.instance.Refresh(_agentList[0]);
		_camera.Target = _agentList[0].transform;
	}

	public void Save()
	{
		List<NeuralNetwork> nets = new List<NeuralNetwork>();

		for (int i = 0; i < _agentList.Count; i++)
		{
			nets.Add(_agentList[i].Network);
		}

		Data data = new Data
		{
			generation = _numberGeneration,
			nets = nets
		};

		DataManager.instance.Save(data);
	}

	public void Load()
	{
		Data data = DataManager.instance.LoadFromFile();

		if (data == null)
			return;

		_numberGeneration = data.generation;

		AddOrRemoveAgent();

		for (int i = 0; i < data.nets.Count && i < _agentList.Count; i++)
		{
			_agentList[i].Network = data.nets[i];
		}

		ResetGeneration();
	}

	public void ActivateVisual()
	{
		Camera cam = _camera.GetComponent<Camera>();
		
		if (_toggleBestAgent.isOn)
		{
			cam.cullingMask &= ~(1 << LayerMask.NameToLayer("Car"));
		}
		else
		{
			cam.cullingMask |= (1 << LayerMask.NameToLayer("Car"));
		}
	}
	
	void SetLayerRecursively(GameObject obj, int newLayer)
	{
		foreach (Transform t in obj.GetComponentsInChildren<Transform>(true))
		{
			t.gameObject.layer = newLayer;
		}
	}
	
	#endregion
}
