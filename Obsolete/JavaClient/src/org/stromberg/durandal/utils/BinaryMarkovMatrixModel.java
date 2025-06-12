/*
 * To change this template, choose Tools | Templates
 * and open the template in the editor.
 */
package org.stromberg.durandal.utils;

import java.io.File;
import java.io.IOException;
import java.util.Scanner;

/**
 *
 * @author lostromb
 */
public class BinaryMarkovMatrixModel
{
    private double[][][] _likelihoods;
    private double[][][] _counts;
    private int _length;
    private int _width;
    private double _trainedThreshold = 0;
    private boolean _isTrained = false;

    public BinaryMarkovMatrixModel(int length, int width)
    {
        _length = length - 1;
        _width = width;
        _likelihoods = new double[_length][][];
        _counts = new double[_length][][];
        for (int x = 0; x < _length; x++)
        {
            _likelihoods[x] = new double[_width][];
            _counts[x] = new double[_width][];
            for (int y = 0; y < _width; y++)
            {
                _likelihoods[x][y] = new double[_width];
                _counts[x][y] = new double[_width];
            }
        }
    }

    /// <summary>
    /// Read a serialized model from a file
    /// </summary>
    /// <param name="fileName"></param>
    public BinaryMarkovMatrixModel(String fileName)
    {
        try
        {
            Scanner reader = new Scanner(new File(fileName));
            _length = reader.nextInt() - 1;
            _width = reader.nextInt();

            _likelihoods = new double[_length][][];
            _counts = new double[_length][][];
            for (int x = 0; x < _length; x++)
            {
                _likelihoods[x] = new double[_width][];
                _counts[x] = new double[_width][];
                for (int y = 0; y < _width; y++)
                {
                    _likelihoods[x][y] = new double[_width];
                    _counts[x][y] = new double[_width];
                }
            }

            _trainedThreshold = reader.nextDouble();
            for (int x = 0; x < _length; x++)
            {
                for (int y = 0; y < _width; y++)
                {
                    for (int z = 0; z < _width; z++)
                    {
                        _likelihoods[x][y][z] = reader.nextDouble();
                        _counts[x][y][z] = reader.nextDouble();
                    }
                }
            }
            reader.close();
            _isTrained = true;
        }
        catch (IOException e)
        {
            System.err.println("Exception while loading markov model: " + e.getMessage());
        }
    }

    private int bucketFunc(double input)
    {
        return Math.min(_width, (int)Math.floor(input * _width));
    }

    private double evaluateInternal(double[] vector)
    {
        if (vector == null)
            throw new NullPointerException("Markov model input vector cannot be null");
        if (vector.length != _length + 1)
            throw new ArrayIndexOutOfBoundsException("Input vector does not match markov model length");

        double returnVal = 0.0;
        int current = bucketFunc(vector[0]);
        for (int c = 0; c < _length; c++)
        {
            int dest = bucketFunc(vector[c + 1]);
            if (_counts[c][current][dest] > 0)
            {
                returnVal += (_likelihoods[c][current][dest] / _counts[c][current][dest]);
            }
            current = dest;
        }
        
        //System.out.println(returnVal);

        return returnVal;
    }

    public boolean evaluate(double[] vector)
    {
        if (!_isTrained)
            throw new IllegalStateException("Markov model is not trained yet!");
        return evaluateInternal(vector) > _trainedThreshold;
    }
}
