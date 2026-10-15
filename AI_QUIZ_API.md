# AI quiz endpoint expected by the Unity client

The app sends a UTF-8 JSON `POST` request to the endpoint configured on the `Manager` component in the Unity scene Inspector. Students only enter their grade; server address and access token are not shown in the app. Keep provider API keys on the backend and do not put them in the Unity app.

## Run the included backend

The Node.js backend uses built-in modules and reads `.env` from the project root. From the project root, run:

```powershell
node backend/server.mjs
```

For Unity Editor on the same computer, set `aiServerEndpoint` to `http://127.0.0.1:8787/quiz`. To connect a phone over the same LAN, set `QUIZ_HOST=0.0.0.0` and a random `QUIZ_ACCESS_TOKEN` of at least 24 characters in the server `.env`, restart the server, then set `aiServerEndpoint` to `http://<computer-LAN-IP>:8787/quiz` and `aiServerAccessToken` to the matching token on the scene's `Manager` component before building the app. A token embedded in a client build can be extracted, so it is only a basic access check. Do not expose this development server directly to the public internet; use a deployed HTTPS backend with real user authentication for production.

The Unity client sends the optional server token as an `Authorization: Bearer ...` header. The token is stored in the scene configuration, not entered by the student.

The wrong-answer screen also posts to `/wrong-summary` with the student's grade and wrong-answer records. The backend returns `summaries`, with one item for each subject and `summary`, `patterns`, and `nextSteps` fields. The server analyzes recurring evidence and avoids claiming a pattern when there are too few answers.

Written quiz answers are posted to `/grade-answer` with the question format, student answer, model answer, grading criteria, and grade. The grader accepts semantically equivalent wording and returns `isCorrect` with brief Korean feedback. For essays, it checks the essential grading criteria rather than requiring the model answer verbatim.

## Request

```json
{
  "subject": "과학",
  "area": "전체 영역",
  "grade": "중학교 2학년",
  "difficulty": "중",
  "questionCount": 10,
  "multipleChoiceCount": 5,
  "essayCount": 2,
  "shortAnswerCount": 3,
  "instructions": "...",
  "materials": [
    {
      "area": "개념",
      "kind": "unit",
      "title": "물질의 상태 변화",
      "content": "입력한 학습 메모 전체",
      "highlights": ["형광펜으로 표시한 핵심 문장"]
    }
  ]
}
```

For Korean and English, `area` is the selected area or `전체 영역`. For science and history/social studies, it is always `전체 영역`. Each material preserves its own area, title, full note or vocabulary content, and highlighted excerpts.

## Response

Return an object with a `questions` array. `format` is `multiple_choice`, `essay`, or `short_answer`. Multiple-choice `answerIndex` is zero based and points to one of four `options`. Written questions use an empty `options` array, `answerIndex: -1`, and a model answer in `answer`; essays also include scoring points in `gradingCriteria`.

```json
{
  "questions": [
    {
      "question": "문제 내용",
      "format": "multiple_choice",
      "options": ["보기 1", "보기 2", "보기 3", "보기 4"],
      "answerIndex": 1,
      "answer": "",
      "gradingCriteria": [],
      "explanation": "정답인 이유",
      "type": "핵심 개념",
      "area": "개념",
      "sourceTitle": "물질의 상태 변화"
    }
  ]
}
```

`type`, `area`, `sourceTitle`, and `explanation` may be empty. Multiple-choice questions need four options and a valid `answerIndex`; written questions need a model answer, and essays need scoring criteria. The requested counts must total between 1 and 20. The endpoint should return HTTP 2xx and `Content-Type: application/json`.
