using System;
using UnityEngine;
using Random = UnityEngine.Random;

[Serializable]
public class NeuralNetwork
{
	#region Variables

	public int[] _layers = {7, 6, 6, 3};
	public float[][] _neurons;
	public float [][][] _axones;

	private int x;
	private int y;
	private int z;
	private int yPrevious;
	
	#endregion
	
	#region Fonctions

	public NeuralNetwork() { }

	public NeuralNetwork(int[] layersModel)
	{
		_layers = new int[layersModel.Length];

		for (x = 0; x < layersModel.Length; x++)
		{
			_layers[x] = layersModel[x];
		}

		InitNeurons();
		InitAxones();
	}

	void InitNeurons()
	{
		_neurons =  new float[_layers.Length][];

		for (x = 0; x < _layers.Length; x++)
		{
			_neurons[x] = new float[_layers[x]];
		}
	}

	private void InitAxones()
	{
		int size = _layers.Length - 1;
		_axones = new float[size][][];

		for (x = 0; x < size; x++)
		{
			_axones[x] = new float[_layers[x]][];

			for (y = 0; y < _axones[x].Length; y++)
			{
				_axones[x][y] = new float[_layers[x+1]];
				
				for (z = 0; z < _axones[x][y].Length; z++)
				{
					_axones[x][y][z] = Random.Range(-1.0f, 1.0f);
				}
			}
		}
	}

	public void FeedForward(float[] input)
	{
		if (input.Length != _neurons[0].Length) return;

		for (x = 0; x < _neurons[0].Length; x++)
		{
			_neurons[0][x] = input[x];
		}

		for (x = 1; x < _layers.Length; x++)
		{
			for (y = 0; y < _neurons[x].Length; y++)
			{
				_neurons[x][y] = GetNewValue(y, _neurons[x-1], _axones[x-1]);
			}
		}
	}

	float GetNewValue(int indexAxones, float[] valueNeurones, float[][] valueAxones)
	{
		float result = 0;

		for (yPrevious = 0; yPrevious < valueNeurones.Length; yPrevious++)
		{
			result += valueAxones[yPrevious][indexAxones] * valueNeurones[yPrevious];
		}
		
		return (float) Math.Tanh(result);
	}

	public void CopyNet(NeuralNetwork netCopy)
	{
		for (x = 0; x < _axones.Length; x++)
		{
			for (y = 0; y < _axones[x].Length; y++)
			{
				for (z = 0; z < _axones[x][y].Length; z++)
				{
					_axones[x][y][z] = netCopy._axones[x][y][z];
				}
			}
		}
	}

	public void Mutate(float probability, float power)
	{
		for (x = 0; x < _axones.Length; x++)
		{
			for(y = 0; y < _axones[x].Length; y++)
			{
				for (z = 0; z < _axones[x][y].Length; z++)
				{
					if (Random.value < probability)
					{
						_axones[x][y][z] += Random.Range(-power, power);
					}
				}
			}
		}
	}
	
	#endregion
}
