using TMPro;
using UnityEngine;
using UnityEngine.UI;

public struct NeuronDisplay
{
	public GameObject    go;
	public RectTransform rectTransform;
	Image                image;
	TMP_Text             text;

	public void Init(float xPox, float yPos)
	{
		rectTransform                  = go.GetComponent<RectTransform>();
		image                          = go.GetComponent<Image>();
		text                           = go.GetComponentInChildren<TMP_Text>();
		rectTransform.anchoredPosition = new Vector2(xPox, yPos);
	}

	public void Refresh(float value, Color color)
	{
		text.text   = value.ToString("F2");
		image.color = color;
	}
}

public struct AxonDisplay
{
	public GameObject go;
	public Image      image;
	RectTransform     rectTransform;

	public void Init(RectTransform start, RectTransform end, float thickness, float neuronDiameter)
	{
		rectTransform                  = go.GetComponent<RectTransform>();
		image                          = go.GetComponent<Image>();
		rectTransform.anchoredPosition = start.anchoredPosition + (end.anchoredPosition                        - start.anchoredPosition) * .5f;
		rectTransform.sizeDelta        = new Vector2((end.anchoredPosition - start.anchoredPosition).magnitude - neuronDiameter, thickness);
		rectTransform.rotation         = Quaternion.FromToRotation(rectTransform.right, (end.anchoredPosition - start.anchoredPosition).normalized);
		rectTransform.SetAsFirstSibling();
	}
}

public class NeuralNetworkViewer : MonoBehaviour
{
	[SerializeField] float    layerSpacing          = 100;
	[SerializeField] float    neuronVerticalSpacing = 32;
	[SerializeField] float    neuronDiameter        = 32;
	[SerializeField] float    axonThickness         = 2;
	[SerializeField] Gradient colorGradient;

	[SerializeField] GameObject    neuronPrefab;
	[SerializeField] GameObject    axonPrefab;
	[SerializeField] GameObject    fitnessPrefab;
	[SerializeField] RectTransform viewGroup;

	public Agent  agent;
	NeuralNetwork net;

	NeuronDisplay[][] neurons;
	AxonDisplay[][][] axons;
	TMP_Text          fitnessDisplay;

	bool  initialised;
	int   maxNeurons;
	float padding;

	int x;
	int y;
	int z;

	public static NeuralNetworkViewer instance;

	void Awake()
	{
		instance = this;
	}

	public void Refresh(Agent agentToLook)
	{
		agent = agentToLook;
		net   = agent.Network;

		if (!initialised)
		{
			initialised = true;
			Init();
		}

		RefreshAxons();
	}

	void Init()
	{
		InitMaxNeurons();
		InitNeurons();
		InitAxons();
		InitFitness();
	}

	void InitMaxNeurons()
	{
		for (x = 0; x < net._layers.Length; x++)
		{
			if (net._layers[x] > maxNeurons)
			{
				maxNeurons = net._layers[x];
			}
		}
	}

	void InitNeurons()
	{
		neurons = new NeuronDisplay[net._layers.Length][];

		for (x = 0; x < net._layers.Length; x++)
		{
			if (net._layers[x] < maxNeurons)
			{
				padding = (maxNeurons - net._layers[x]) * .5f * neuronVerticalSpacing;

				if (net._layers[x] % 2 != maxNeurons % 2)
				{
					padding += neuronVerticalSpacing * .5f;
				}
			}
			else
			{
				padding = 0;
			}

			neurons[x] = new NeuronDisplay[net._layers[x]];

			for (y = 0; y < net._layers[x]; y++)
			{
				neurons[x][y]    = new NeuronDisplay();
				neurons[x][y].go = Instantiate(neuronPrefab, viewGroup);
				neurons[x][y].Init(x * layerSpacing, -padding - neuronVerticalSpacing * y);
			}
		}
	}

	void InitAxons()
	{
		axons = new AxonDisplay[net._layers.Length - 1][][];

		for (x = 0; x < net._layers.Length - 1; x++)
		{
			axons[x] = new AxonDisplay[net._layers[x]][];

			for (y = 0; y < net._layers[x]; y++)
			{
				axons[x][y] = new AxonDisplay[net._layers[x + 1]];

				for (z = 0; z < net._layers[x + 1]; z++)
				{
					axons[x][y][z]    = new AxonDisplay();
					axons[x][y][z].go = Instantiate(axonPrefab, viewGroup);

					axons[x][y][z].Init(neurons[x][y].rectTransform, neurons[x + 1][z].rectTransform, axonThickness, neuronDiameter);
				}
			}
		}
	}

	void InitFitness()
	{
		GameObject fitness = Instantiate(fitnessPrefab, viewGroup);
		fitness.GetComponent<RectTransform>().anchoredPosition = new Vector2(net._layers.Length * layerSpacing, -maxNeurons * .5f * neuronVerticalSpacing);
		fitnessDisplay                                         = fitness.GetComponent<TMP_Text>();
	}
	
	void RefreshAxons()
	{
		for (x = 0; x < axons.Length; x++)
		{
			for ( y = 0; y < axons[x].Length; y++)
			{
				for (z = 0; z < axons[x][y].Length; z++)
				{
					axons[x][y][z].image.color = colorGradient.Evaluate((net._axones[x][y][z] + 1) * .5f);
				}
			}
		}
	}

	void Update()
	{
		for (x = 0; x < neurons.Length; x++)
		{
			for ( y = 0; y < neurons[x].Length; y++)
			{
				neurons[x][y].Refresh(net._neurons[x][y],colorGradient.Evaluate((net._neurons[x][y] + 1)*.5f));				
			}
		}

		fitnessDisplay.text = agent.Fitness.ToString("F1");
	}
}