﻿using System;
using System.Collections.Generic;
using System.Linq;
using Souvenir;
using UnityEngine;

public partial class SouvenirModule
{
    private IEnumerator<YieldInstruction> ProcessKanji(ModuleData module)
    {
        var comp = GetComponent(module, "KanjiModule");

        var fldCalculating = GetField<bool>(comp, "Calculating");
        var fldStage = GetIntField(comp, "Stage");
        var screen = GetField<TextMesh>(comp, "ScreenText", isPublic: true).Get();
        var displayedWords = new string[3];
        var stage = 0;

        module.Module.OnStrike += () => { stage--; return false; }; // Grab the text on the screen again on strike.

        while (screen.text == "爆発")
            yield return null; // Don’t wait .1 seconds so that we are absolutely sure we get the right stage. (yes I stole this comment :D)
        displayedWords[0] = screen.text;

        for (stage = 1; stage <= 2 || module.Unsolved; stage++)
        {
            while (fldStage.Get(min: stage, max: stage + 1) == stage)
                yield return new WaitForSeconds(.1f); // Stage animation takes much longer than .1 seconds anyway.
            while (fldCalculating.Get())
                yield return null; // Don’t wait .1 seconds so that we are absolutely sure we get the right stage.
            if (stage < 3)
                displayedWords[stage] = screen.text;
            yield return new WaitForSeconds(.1f); // Keep looping until solve here so we can still grab the text in the event of a strike on the last stage.
        }

        var wordLists = new string[][]
        {
            GetArrayField<string>(comp, "Stage1Words").Get(arr => !arr.Contains(displayedWords[0]) ? $"expected array to contain \"{displayedWords[0]}\"" : null),
            GetArrayField<string>(comp, "Stage2Char").Get(arr => !arr.Contains(displayedWords[1]) ? $"expected array to contain \"{displayedWords[1]}\"" : null),
            GetArrayField<string>(comp, "Stage3Words").Get(arr => !arr.Contains(displayedWords[2]) ? $"expected array to contain \"{displayedWords[2]}\"" : null)
        };
        addQuestions(module, Enumerable.Range(0, 3).Select(stage => makeQuestion(Question.KanjiDisplayedWords, module, formatArgs: new[] { ordinal(stage + 1) }, correctAnswers: new[] { displayedWords[stage] }, preferredWrongAnswers: wordLists[stage])));
    }

    private IEnumerator<YieldInstruction> ProcessKanyeEncounter(ModuleData module)
    {
        var comp = GetComponent(module, "TheKanyeEncounter");

        var fldFoodsAvailable = GetArrayField<int>(comp, "FooderPickerNumberSelector");
        var foodNames = GetField<string[]>(comp, "FoodsButCodeText").Get();
        for (int i = 0; i < foodNames.Length; i++)
            if (foodNames[i] == "Corn [inedible]")
                foodNames[i] = "Corn";

        yield return WaitForSolve;

        var selectedFoods = fldFoodsAvailable.Get(expectedLength: 3);
        var selectedFoodNames = selectedFoods.Select(x => foodNames[x]).ToArray();
        addQuestion(module, Question.KanyeEncounterFoods, correctAnswers: selectedFoodNames);
    }

    private IEnumerator<YieldInstruction> ProcessKeypadCombination(ModuleData module)
    {
        var comp = GetComponent(module, "KeypadCombinations");
        yield return WaitForSolve;

        var buttonNums = GetField<int[,]>(comp, "buttonnum").Get(v => v.GetLength(0) != 4 || v.GetLength(1) != 3 ? "expected 4×3 array" : null);
        var moduleAnswer = GetField<string>(comp, "answer").Get();

        addQuestions(module, Enumerable.Range(0, 4).Select(i => makeQuestion(Question.KeypadCombinationWrongNumbers, module,
            formatArgs: new[] { ordinal(i + 1) },
            correctAnswers: Enumerable.Range(0, 3).Select(buttonIndex => buttonNums[i, buttonIndex])
                .Where(num => num != moduleAnswer[i] - '0').Select(num => num.ToString()).ToArray())));
    }

    private IEnumerator<YieldInstruction> ProcessKeypadMagnified(ModuleData module)
    {
        var comp = GetComponent(module, "KeypadMagnifiedScript");

        var LEDPos = GetIntField(comp, "chosenPosition").Get(min: 0, max: 3);
        yield return WaitForSolve;

        var posNames = new[] { "Top-left", "Top-right", "Bottom-left", "Bottom-right" };
        addQuestion(module, Question.KeypadMagnifiedLED, correctAnswers: new[] { posNames[LEDPos] });
    }

    private IEnumerator<YieldInstruction> ProcessKeywords(ModuleData module)
    {
        var comp = GetComponent(module, "keywordsScript");

        yield return WaitForSolve;

        var consonants = "bcdfghjklmnpqrstvwxyz".ToCharArray();
        var vowels = "aeiou".ToCharArray();
        var displayedKey = GetField<string>(comp, "displayKey").Get(v => v.Length < 4 ? "expected length at least 4" : null).Substring(0, 4);
        if (displayedKey.Count(x => consonants.Contains(x)) != 2 || displayedKey.Count(x => vowels.Contains(x)) != 2)
            throw new AbandonModuleException($"‘displayKey’ had an unexpected value of “{displayedKey}” when I expected a string starting with two consonants and two vowels in any order.");

        var possibleAnswers = new HashSet<string>() { displayedKey };
        while (possibleAnswers.Count < 6)
            possibleAnswers.Add(consonants.Shuffle().Take(2).Concat(vowels.Shuffle().Take(2)).ToArray().Shuffle().JoinString());

        addQuestion(module, Question.KeywordsDisplayedKey, correctAnswers: new[] { displayedKey }, preferredWrongAnswers: possibleAnswers.ToArray());
    }

    private IEnumerator<YieldInstruction> ProcessKnowYourWay(ModuleData module)
    {
        var comp = GetComponent(module, "KnowYourWayScript");

        yield return WaitForSolve;

        var ledIndex = GetIntField(comp, "LEDLoc").Get(min: 0, max: 3);
        var arrowIndex = GetIntField(comp, "ArrowLoc").Get(min: 0, max: 3);
        GetArrayField<GameObject>(comp, "Bars", isPublic: true)
            .Get(expectedLength: 4)[ledIndex]
            .GetComponent<MeshRenderer>().material = GetArrayField<Material>(comp, "LEDs", isPublic: true).Get(expectedLength: 2)[0];

        addQuestions(
            module,
            makeQuestion(Question.KnowYourWayArrow, module, correctAnswers: new[] { new[] { "Up", "Left", "Down", "Right" }[arrowIndex] }),
            makeQuestion(Question.KnowYourWayLed, module, correctAnswers: new[] { new[] { "Top", "Left", "Bottom", "Right" }[ledIndex] })
        );
    }

    private IEnumerator<YieldInstruction> ProcessKudosudoku(ModuleData module)
    {
        var comp = GetComponent(module, "KudosudokuModule");
        var shown = GetArrayField<bool>(comp, "_shown").Get(expectedLength: 16).ToArray();  // Take a copy of the array because the module changes it

        yield return WaitForSolve;

        addQuestions(module,
            makeQuestion(Question.KudosudokuPrefilled, module, formatArgs: new[] { "pre-filled" },
                correctAnswers: Enumerable.Range(0, 16).Where(ix => shown[ix]).Select(coord => new Coord(4, 4, coord)).ToArray()),
            makeQuestion(Question.KudosudokuPrefilled, module, formatArgs: new[] { "not pre-filled" },
                correctAnswers: Enumerable.Range(0, 16).Where(ix => !shown[ix]).Select(coord => new Coord(4, 4, coord)).ToArray()));
    }

    private IEnumerator<YieldInstruction> ProcessKyudoku(ModuleData module)
    {
        var comp = GetComponent(module, "KyudokuScript");

        yield return WaitForSolve;

        var info = GetField<object>(comp, "puzzleInfo").Get();
        var given = GetField<int>(info, "Given", isPublic: true).Get(v => v is < 0 or > 35 ? $"Bad given digit location {v}" : null);
        var digit = GetArrayField<int>(info, "NumberGrid", isPublic: true).Get(expectedLength: 36, validator: v => v is < 1 or > 9 ? $"Bad grid digit {v}" : null)[given];
        var os = GetArrayField<bool>(info, "Solution", isPublic: true).Get(expectedLength: 36);

        static IEnumerable<int> allIndices(bool[] arr)
        {
            var lastIndex = -1;
            do
            {
                lastIndex = Array.IndexOf(arr, true, lastIndex + 1);
                if (lastIndex != -1)
                    yield return lastIndex;
            }
            while (lastIndex != -1);
        }

        var allCircled = allIndices(os).ToArray();
        if (allCircled.Length != 9)
            throw new AbandonModuleException($"Expected 9 circled digits but found {allCircled.Length}. ({allCircled.JoinString(", ")})");

        addQuestions(module,
            makeQuestion(Question.KyudokuGivenDigit, module, correctAnswers: new[] { digit.ToString() }),
            makeQuestion(Question.KyudokuGivenDigitLocation, module, correctAnswers: new[] { new Coord(6, 6, given) }, preferredWrongAnswers: allCircled.Select(c => new Coord(6, 6, c)).ToArray()));
    }
}
