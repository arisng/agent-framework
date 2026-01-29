# Quiz Card Data Model

This document provides a compact, copy‑pasteable data model for rendering a quiz as question cards, including a single card’s details: question, answers, correct answers (conditional display), and user choices.

## 1) Core Concepts

- **Quiz** contains multiple **Question Cards**.
- **Question Card** describes one question, its answers, how many can be selected, and whether correct answers should be shown.
- **User Choices** are captured on a per‑card basis and can be empty while the user is still answering.

## 2) Type Definitions (TypeScript‑style)

```ts
export type Quiz = {
  id: string;
  title: string;
  instructions?: string;
  cards: QuestionCard[];
};

export type QuestionCard = {
  id: string;
  sequence: number;
  question: QuestionContent;
  answers: AnswerOption[];

  // Selection rules
  selection: SelectionRule;

  // Correct answer data (always present for grading)
  correctAnswerIds: string[];

  // Presentation rules for correct answers
  correctAnswerDisplay: CorrectAnswerDisplayRule;

  // User choices (empty if not answered yet)
  userChoiceIds: string[];

  // Optional computed flags for rendering
  evaluation?: CardEvaluation;
};

export type QuestionContent = {
  text: string;
  description?: string;
  mediaUrl?: string;
};

export type AnswerOption = {
  id: string;
  text: string;
  description?: string;
  mediaUrl?: string;
  isDisabled?: boolean;
};

export type SelectionRule = {
  mode: "single" | "multiple";
  minSelections?: number; // default 1
  maxSelections?: number; // default 1 for single
};

export type CorrectAnswerDisplayRule = {
  // When to show correct answers in the UI
  visibility: "never" | "afterSubmit" | "afterReveal" | "always";

  // Optional front‑end control to show a reveal button
  allowReveal?: boolean;
};

export type CardEvaluation = {
  isCorrect: boolean;
  // True if all correct answers chosen and no extra selections
  score?: number; // 0..1, optional
  feedback?: string;
};
```

## 3) JSON Example (Single Quiz with Two Cards)

```json
{
  "id": "quiz-001",
  "title": "Basic Safety Quiz",
  "instructions": "Answer all questions. You can review results at the end.",
  "cards": [
    {
      "id": "card-001",
      "sequence": 1,
      "question": {
        "text": "Which item is required PPE on a construction site?",
        "description": "Select one option.",
        "mediaUrl": null
      },
      "answers": [
        { "id": "a1", "text": "Safety helmet" },
        { "id": "a2", "text": "Flip‑flops" },
        { "id": "a3", "text": "Sunglasses" }
      ],
      "selection": { "mode": "single", "minSelections": 1, "maxSelections": 1 },
      "correctAnswerIds": ["a1"],
      "correctAnswerDisplay": { "visibility": "afterSubmit", "allowReveal": false },
      "userChoiceIds": ["a1"],
      "evaluation": { "isCorrect": true, "score": 1 }
    },
    {
      "id": "card-002",
      "sequence": 2,
      "question": {
        "text": "Select all fire‑safety practices:",
        "description": "Multiple selections are allowed.",
        "mediaUrl": null
      },
      "answers": [
        { "id": "b1", "text": "Keep exits clear" },
        { "id": "b2", "text": "Block fire doors open" },
        { "id": "b3", "text": "Store flammables safely" },
        { "id": "b4", "text": "Ignore fire drills" }
      ],
      "selection": { "mode": "multiple", "minSelections": 1, "maxSelections": 3 },
      "correctAnswerIds": ["b1", "b3"],
      "correctAnswerDisplay": { "visibility": "afterReveal", "allowReveal": true },
      "userChoiceIds": ["b1", "b2"],
      "evaluation": { "isCorrect": false, "score": 0.5, "feedback": "One choice is incorrect." }
    }
  ]
}
```

## 4) Rendering Notes

- **Conditional correct answers**: Use `correctAnswerDisplay.visibility` to decide when to show correct answers.
- **User choices**: Use `userChoiceIds` to highlight selected options.
- **Scoring**: If you compute grading client‑side, store results in `evaluation`.

## 5) Minimal Card Shape (If you want a leaner model)

```ts
export type MinimalCard = {
  id: string;
  question: string;
  answers: { id: string; text: string }[];
  correctAnswerIds: string[];
  showCorrectAnswers: boolean;
  userChoiceIds: string[];
};
```
