using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using Rnd = UnityEngine.Random;

public class PuzzlePanelScript : MonoBehaviour
{
    const int STAGE_1_FLIPS = 3, STAGE_2_FLIPS = 4;
    public KMBombModule Module;
    public KMBombInfo BombInfo;
    public KMAudio Audio;
    public KMSelectable[] SquareSels;
    public GameObject[] SquareObjs;
    public Renderer[] SquareFronts, SquareBacks;
    public Material[] SquareMats;
    public TextMesh ScreenText;
    public Renderer StageLed;
    public Material[] StageLedMats;

    private int _moduleId;
    private static int _moduleIdCounter = 1;
    private bool _moduleSolved;

    private bool _isAnimating;
    private bool _isInputting;
    private bool[] _isFlippedUp = new bool[16];
    private bool[] _isFirstSolFlipped = new bool[16];
    private bool[] _isSecondSolFlipped = new bool[16];
    private string[] _sounds = { "Click1", "Click2", "Click3" };

    private int[][] _adjacents = new int[16][]
    {
        new int[4] {0, 1, 4, 5},
        new int[6] {0, 1, 2, 4, 5, 6},
        new int[6] {1, 2, 3, 5, 6, 7},
        new int[4] {2, 3, 6, 7},

        new int[6] {0, 1, 4, 5, 8, 9},
        new int[9] {0, 1, 2, 4, 5, 6, 8, 9, 10},
        new int[9] {1, 2, 3, 5, 6, 7, 9, 10, 11},
        new int[6] {2, 3, 6, 7, 10, 11},

        new int[6] {4, 5, 8, 9, 12, 13},
        new int[9] {4, 5, 6, 8, 9, 10, 12, 13, 14},
        new int[9] {5, 6, 7, 9, 10, 11, 13, 14, 15},
        new int[6] {6, 7, 10, 11, 14, 15},

        new int[4] {8, 9, 12, 13},
        new int[6] {8, 9, 10, 12, 13, 14},
        new int[6] {9, 10, 11, 13, 14, 15},
        new int[4] {10, 11, 14, 15},
    };
    private bool[][] patterns = new[]
    {
        "oxxoxxxxoxxoxoox",
        "xooxoxxooxxoxoox",
        "ooooxxxxooooxxxx",
        "oxoxoxoxoxoxoxox",
        "xxxxxooxxooxxxxx",
        "oxoxxoxooxoxxoxo",
        "ooxxoooxxoooxxoo",
        "xxooxxooooxxooxx",
        "oxxoxxxxxxxxoxxo",
        "xxxxooxxxxooxxxx",
        "xxoxoooxxoooxoxx",
        "xooxoxxooooooxxo",
    }.Select(str => str.Select(ch => ch == 'o').ToArray()).ToArray();

    private List<int> _stageOne = new List<int>();
    private List<int> _stageTwo = new List<int>();
    private bool[] thisStagePattern;

    private int _stageNum = 0;
    private int _movesLeft;

    private bool _correct;

    private List<string> _flippedSquares = new List<string>();
    private List<string> _possibleMoves = new List<string>();

    private void Start()
    {
        _moduleId = _moduleIdCounter++;
        for (int i = 0; i < SquareSels.Length; i++)
            SquareSels[i].OnInteract += SquarePress(i);
        StageLed.material = StageLedMats[0];

        _stageOne = Enumerable.Range(0, 16).ToArray().Shuffle().Take(STAGE_1_FLIPS).ToList();
        _stageTwo = Enumerable.Range(0, 16).ToArray().Shuffle().Take(STAGE_2_FLIPS).ToList();
        
        ScreenText.text = STAGE_1_FLIPS.ToString();
        _movesLeft = STAGE_1_FLIPS;

        thisStagePattern = patterns.PickRandom();
        for (int i = 0; i < 16; i++)
            if (thisStagePattern[i])
                FlipOverSingle(i, true);

        for (int i = 0; i < _stageOne.Count; i++)
        {
            StartCoroutine(FlipSquare(_stageOne[i], 0f, true));
            _possibleMoves.Add("ABCD".Substring(_stageOne[i] % 4, 1) + "1234".Substring(_stageOne[i] / 4, 1));
        }
        for (int i = 0; i < 16; i++)
        {
            if (_isFirstSolFlipped[i])
                _flippedSquares.Add("ABCD".Substring(i % 4, 1) + "1234".Substring(i / 4, 1));
        }
        Debug.LogFormat("[Puzzle Panel #{0}] Stage 1: With {1} flips, squares {2} have been flipped over.", _moduleId, STAGE_1_FLIPS, _flippedSquares.Join(" "));
        Debug.LogFormat("[Puzzle Panel #{0}] Possible solution path: {1}", _moduleId, _possibleMoves.Join(", "));
    }

    private KMSelectable.OnInteractHandler SquarePress(int sq)
    {
        return delegate ()
        {
            if (!_moduleSolved && !_isAnimating)
            {
                if (!_isInputting)
                {
                    StartCoroutine(FlipToSolution(_stageNum, false));
                }
                else
                {
                    StartCoroutine(FlipSquare(sq, 0.4f, false));
                    _movesLeft--;
                    ScreenText.text = _movesLeft.ToString();
                }
            }
            return false;
        };
    }

    private IEnumerator FlipSquare(int sq, float dur, bool isGen)
    {
        _isAnimating = true;
        Audio.PlaySoundAtTransform(_sounds.PickRandom(), transform);
        var duration = dur;
        var elapsed = 0f;
        while (elapsed < duration)
        {
            for (int i = 0; i < _adjacents[sq].Length; i++)
                SquareObjs[_adjacents[sq][i]].transform.localEulerAngles = new Vector3(0f, 0f, Easing.InOutQuad(elapsed, 0f, 180f, duration));
            yield return null;
            elapsed += Time.deltaTime;
        }
        foreach (int ix in _adjacents[sq])
            FlipOverSingle(ix, isGen);
        _isAnimating = false;
        _correct = true;
        for (int i = 0; i < 16; i++)
        {
            if (_stageNum == 0)
                if (_isFlippedUp[i] != _isFirstSolFlipped[i])
                    _correct = false;
            if (_stageNum == 1)
                if (_isFlippedUp[i] != _isSecondSolFlipped[i])
                    _correct = false;
        }
        if (_correct)
        {
            Debug.LogFormat("[Puzzle Panel #{0}] Got to the solution in the required amount of moves.", _moduleId);
            Audio.PlaySoundAtTransform("Correct", transform);
            if (_stageNum == 0)
            {
                Debug.LogFormat("[Puzzle Panel #{0}] Moving onto Stage 2.", _moduleId);
                DoStageTwo();
            }
            else
            {
                _moduleSolved = true;
                Module.HandlePass();
                Debug.LogFormat("[Puzzle Panel #{0}] Module solved.", _moduleId);
                StageLed.material = StageLedMats[2];
            }
        }
        if (_movesLeft <= 0 && !_correct)
        {
            StartCoroutine(FlipToSolution(_stageNum, true));
            _isInputting = false;
        }
        if (_movesLeft != 0)
            _isAnimating = false;
    }

    private void FlipOverSingle(int ix, bool isGen)
    {
        SquareObjs[ix].transform.localEulerAngles = new Vector3(0f, 0f, 0f);
        if (!isGen)
        {

            _isFlippedUp[ix] = !_isFlippedUp[ix];
            SquareFronts[ix].material = _isFlippedUp[ix] ? SquareMats[1] : SquareMats[0];
            SquareBacks[ix].material = _isFlippedUp[ix] ? SquareMats[0] : SquareMats[1];
        }
        else
        {
            if (_stageNum == 0)
            {
                _isFirstSolFlipped[ix] = !_isFirstSolFlipped[ix];
                SquareFronts[ix].material = _isFirstSolFlipped[ix] ? SquareMats[1] : SquareMats[0];
                SquareBacks[ix].material = _isFirstSolFlipped[ix] ? SquareMats[0] : SquareMats[1];
            }
            else
            {
                _isSecondSolFlipped[ix] = !_isSecondSolFlipped[ix];
                SquareFronts[ix].material = _isSecondSolFlipped[ix] ? SquareMats[1] : SquareMats[0];
                SquareBacks[ix].material = _isSecondSolFlipped[ix] ? SquareMats[0] : SquareMats[1];
            }
        }
    }

    private void DoStageTwo()
    {
        _movesLeft = 99;
        for (int i = 0; i < _stageOne.Count; i++)
        {
            StartCoroutine(FlipSquare(_stageOne[i], 0f, true));
        }
        _movesLeft = STAGE_2_FLIPS;
        ScreenText.text = _movesLeft.ToString();
        _stageNum++;
        _isInputting = false;
        StageLed.material = StageLedMats[1];

        _possibleMoves = new List<string>();
        _flippedSquares = new List<string>();

        for (int i = 0; i < _stageTwo.Count; i++)
        {
            StartCoroutine(FlipSquare(_stageTwo[i], 0f, true));
            _possibleMoves.Add("ABCD".Substring(_stageTwo[i] % 4, 1) + "1234".Substring(_stageTwo[i] / 4, 1));
        }
        for (int i = 0; i < 16; i++)
        {
            if (_isSecondSolFlipped[i])
                _flippedSquares.Add("ABCD".Substring(i % 4, 1) + "1234".Substring(i / 4, 1));
        }
        Debug.LogFormat("Puzzle Panel #{0}] Stage 2: With {1} flips, squares {2} have been flipped over.", _moduleId, STAGE_2_FLIPS, _flippedSquares.Join(" "));
        Debug.LogFormat("[Puzzle Panel #{0}] Possible solution path: {1}", _moduleId, _possibleMoves.Join(", "));

        _isAnimating = false;
    }

    private IEnumerator FlipToSolution(int solNum, bool toSolution)
    {
        _isAnimating = true;
        Audio.PlaySoundAtTransform(_sounds[Rnd.Range(0, _sounds.Length)], transform);
        var duration = 0.4f;
        var elapsed = 0f;
        for (int i = 0; i < 16; i++)
            if (!toSolution)
                _isFlippedUp[i] = false;
        while (elapsed < duration)
        {
            for (int i = 0; i < 16; i++)
            {
                if ((_stageNum == 0 && _isFlippedUp[i] != _isFirstSolFlipped[i]) || (_stageNum == 1 && _isFlippedUp[i] != _isSecondSolFlipped[i]))
                    SquareObjs[i].transform.localEulerAngles = new Vector3(0f, 0f, Easing.InOutQuad(elapsed, 0f, 180f, duration));
                if ((_stageNum == 0 && _isFlippedUp[i] != _isFirstSolFlipped[i]) || (_stageNum == 1 && _isFlippedUp[i] != _isSecondSolFlipped[i]))
                    SquareObjs[i].transform.localEulerAngles = new Vector3(0f, 0f, Easing.InOutQuad(elapsed, 0f, 180f, duration));
            }
            yield return null;
            elapsed += Time.deltaTime;
        }
        for (int i = 0; i < 16; i++)
        {
            SquareObjs[i].transform.localEulerAngles = new Vector3(0f, 0f, 0f);
            if (!toSolution)
            {
                SquareFronts[i].material = _isFlippedUp[i] ? SquareMats[1] : SquareMats[0];
                SquareBacks[i].material = _isFlippedUp[i] ? SquareMats[0] : SquareMats[1];
            }
            else
            {
                if (_stageNum == 0)
                {
                    if (_isFlippedUp[i] != _isFirstSolFlipped[i])
                        _isFlippedUp[i] = _isFirstSolFlipped[i];
                }
                else
                {
                    if (_isFlippedUp[i] != _isSecondSolFlipped[i])
                        _isFlippedUp[i] = _isSecondSolFlipped[i];
                }
                SquareFronts[i].material = _isFlippedUp[i] ? SquareMats[1] : SquareMats[0];
                SquareBacks[i].material = _isFlippedUp[i] ? SquareMats[0] : SquareMats[1];

            }
        }
        _movesLeft = solNum == 0 ? STAGE_1_FLIPS : STAGE_2_FLIPS;
        ScreenText.text = _movesLeft.ToString();
        _isInputting = !toSolution;
        _isAnimating = false;
    }
    private readonly string[] CoordNames = { "a1", "b1", "c1", "d1", "a2", "b2", "c2", "d2", "a3", "b3", "c3", "d3", "a4", "b4", "c4", "d4" };

    public void LogGrid( object[] grid, int height, int width, string separator = " ")
    {
        for (int row = 0; row < height; row++)
            Debug.Log("[Puzzle Panel #"+_moduleId+"] " + string.Join(separator, Enumerable.Range(row * height, width).Select(x => grid[x].ToString()).ToArray()));
    }

#pragma warning disable 0414
    private readonly string TwitchHelpMessage = "Flip over specific tiles with !{0} A1 B2 C3 D4.";
#pragma warning restore 0414
    private IEnumerator ProcessTwitchCommand(string command)
    {
        var pieces = command.Split(new[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < pieces.Length; i++)
        {
            if (!CoordNames.Contains(pieces[i].ToLowerInvariant()))
                yield break;
            yield return null;
            if (!_isInputting)
            {
                SquareSels[0].OnInteract();
                yield return new WaitForSeconds(0.5f);
            }
            SquareSels[Array.IndexOf(CoordNames, pieces[i])].OnInteract();
            yield return new WaitForSeconds(0.5f);
            if (_correct || _movesLeft == 0)
                yield break;
        }
    }
}
