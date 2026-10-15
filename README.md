# study_app

Unity study app for entering study materials, reviewing saved content, and generating quizzes with an AI backend.

## Project

- Unity Editor: 2022.3.25f1
- Startup scene: `Assets/Scenes/SampleScene.unity`
- AI backend: `backend/server.mjs`
- API setup: [AI_QUIZ_API.md](AI_QUIZ_API.md)

Keep API keys in the local root `.env` file on the backend host. The `.env` file is ignored by Git; `.env.example` is a placeholder only. Android builds must use a reachable HTTPS backend endpoint instead of `127.0.0.1`.