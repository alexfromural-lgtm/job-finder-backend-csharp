# Job Finder — .NET 9 C# Backend

A production-ready ASP.NET Core 9 Web API that is a **drop-in replacement** for the Node.js backend. It connects to the same React frontend with zero frontend configuration changes.

## Tech Stack

| Component | Technology |
|-----------|-----------|
| Runtime | .NET 9 / ASP.NET Core |
| Database | PostgreSQL 16 via Npgsql + EF Core 9 |
| Queue | Redis 7 + Custom `IHostedService` worker |
| Auth | JWT (HMAC-SHA256) via HTTP-only cookies |
| Validation | FluentValidation 11 |
| Containerization | Docker + Docker Compose |

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    job-finder-react                         │
│                                                             │
│  Mode A: Vite dev server (port 3000)                        │
│          └── proxy /api → http://localhost:5002             │
│                                                             │
│  Mode B/C: nginx Docker (port 8080)                         │
│          └── proxy /api → $BACKEND_HOST                     │
│               ├── Docker mode:  http://backend:5002         │
│               └── Hybrid mode:  http://host.docker.internal:5002
└─────────────────────────────────────────────────────────────┘
                           │
              job-finder-backend-csharp (port 5002)
                    ├── PostgreSQL (port 5432)
                    └── Redis (port 6379)
                            └── QueueWorker (IHostedService)
```

## Running Modes

> ⚠️ **Only one backend should run at a time.** Both the Node.js and C# stacks use the same ports and Docker network.

---

### 🐳 Mode A — Full Docker (everything in containers)

All services — backend, PostgreSQL, Redis — run as Docker containers. The React frontend is served by nginx (also in Docker) and proxies API calls to the backend container over the shared Docker network.

**Step 1 — Create the shared Docker network (once)**

```bash
docker network create job-finder-network
```

**Step 2 — Stop the Node.js backend if running**

```bash
# In job-finder-backend-node:
docker compose down
```

**Step 3 — Start the C# backend**

```bash
# In this directory:
docker compose up --build -d
```

**Step 4 — Start the React frontend**

```bash
# In job-finder-react:
docker compose up --build -d
# Open http://localhost:8080
```

**Step 5 — Seed the database (first run only)**

```bash
docker compose exec backend dotnet JobFinder.Api.dll --seed
```

Demo accounts created by seeding:
| Email | Password | Role |
|-------|----------|------|
| `admin@example1.com` | `admin` | ADMIN |
| `recruiter@example.com` | `recruiter123` | RECRUITER |
| `seeker@example.com` | `seeker123` | JOB_SEEKER |

---

### 💻 Mode B — Local Dev (`dotnet run` + Vite dev server)

Best for active development. The backend runs natively for fast iteration; only PostgreSQL and Redis run in Docker. The Vite dev server handles the frontend with HMR.

> **Why set env vars?**
> The `.env` file uses Docker container hostnames (`job-finder-db-csharp`, `job-finder-redis-csharp`) that only resolve inside the Docker network. Shell env vars take priority over `.env`, so setting them to `localhost` overrides the Docker hostnames.

```powershell
# Step 1 — start only DB + Redis
docker compose up postgres redis -d

# Step 2 — run the backend natively (PowerShell)
cd src\JobFinder.Api
$env:DATABASE_URL="Host=localhost;Port=5432;Database=job_finder;Username=job_finder_user;Password=secure_password_123"
$env:REDIS_URL="localhost:6379"
$env:ASPNETCORE_ENVIRONMENT="Development"
dotnet run

# Step 3 — start Vite dev server (in job-finder-react)
npm run dev
# Open http://localhost:3000
```

Expected backend output:
```
✅ Database schema initialized successfully.
🚀 Server is running on port 5002 in Development mode.
```

To seed the database locally:
```powershell
dotnet run -- --seed
```

---

### 🔀 Mode C — Hybrid (native backend + nginx Docker frontend)

Useful when you want the nginx/production frontend container but are running the backend natively (e.g. debugging).

```powershell
# Step 1 — run the backend natively (same as Mode B steps 1–2)

# Step 2 — start the React nginx container pointing at the host machine
cd i:\path\to\job-finder-react
$env:BACKEND_HOST="http://host.docker.internal:5002"
docker compose up --build -d
# Open http://localhost:8080
```

> `host.docker.internal` is Docker's special DNS name that resolves to your Windows/Mac host from inside a container. The `docker-compose.yml` already adds the required `extra_hosts` entry for Linux Docker hosts.

## API Endpoints

All endpoints are identical to the Node.js backend.

### Auth — `/api/auth`
| Method | Path | Auth | Description |
|--------|------|------|-------------|
| POST | `/signup/jobseeker` | — | Register as a job seeker |
| POST | `/signup/recruiter` | — | Register as a recruiter |
| POST | `/login` | — | Login |
| POST | `/logout` | ✓ | Logout (clears cookies) |
| POST | `/refresh` | — | Refresh tokens via cookie |
| POST | `/upgrade` | JOB_SEEKER | Upgrade to recruiter |
| GET | `/me` | ✓ | Get current user |

### Jobs — `/api/jobs`
| Method | Path | Auth | Description |
|--------|------|------|-------------|
| GET | `/all` | — | List jobs (filter: category, location, search, page) |
| GET | `/:id` | — | Get job by ID |
| POST | `/` | RECRUITER | Create job |
| PATCH | `/:id` | RECRUITER | Update job |
| DELETE | `/:id` | RECRUITER | Delete job |
| GET | `/recruiter` | RECRUITER | Get my jobs |

### Job Seeker — `/api/jobseeker`
| Method | Path | Auth | Description |
|--------|------|------|-------------|
| GET | `/profile` | JOB_SEEKER | Get profile |
| PATCH | `/profile` | JOB_SEEKER | Update profile |
| POST | `/apply/:jobId` | JOB_SEEKER | Apply to job (queued → 202) |
| GET | `/applications` | JOB_SEEKER | List my applications |
| DELETE | `/applications/:id` | JOB_SEEKER | Withdraw application |
| POST | `/save/:jobId` | JOB_SEEKER | Save job (queued → 202) |
| GET | `/saved` | JOB_SEEKER | List saved jobs |
| DELETE | `/saved/:jobId` | JOB_SEEKER | Unsave job |

### Recruiter — `/api/recruiter`
| Method | Path | Auth | Description |
|--------|------|------|-------------|
| GET | `/profile` | RECRUITER | Get profile |
| PATCH | `/profile` | RECRUITER | Update profile |
| GET | `/jobs/:jobId/applications` | RECRUITER | List applicants |
| PATCH | `/applications/:id/status` | RECRUITER | Update application status |

### Queue — `/api/queue`
| Method | Path | Auth | Description |
|--------|------|------|-------------|
| GET | `/job/:jobId` | — | Poll background job status |

## Background Queue

Apply-to-job and Save-job requests are written **asynchronously** to the database via a Redis-backed queue, identical to the Node.js Bull queue behaviour:

1. Request is accepted immediately with `202 Accepted` and a `jobId`
2. The `QueueWorker` (`IHostedService`) pops jobs from Redis and writes to PostgreSQL
3. The frontend polls `GET /api/queue/job/:jobId` until `state === "completed"` or `"failed"`
4. Failed jobs are retried up to **3 times** with exponential back-off

## Environment Variables

Copy `.env.sample` to `.env` and fill in the values:

```bash
cp .env.sample .env
```

| Variable | Description |
|----------|-------------|
| `PORT` | API port (default: `5002`) |
| `DATABASE_URL` | Npgsql connection string |
| `REDIS_URL` | Redis host:port |
| `ACCESS_TOKEN_SECRET` | JWT signing secret (min 16 chars) |
| `REFRESH_TOKEN_SECRET` | JWT refresh secret (min 16 chars) |
| `ACCESS_TOKEN_EXPIRES_IN_MINUTES` | Access token TTL in minutes |
| `REFRESH_TOKEN_EXPIRES_IN_DAYS` | Refresh token TTL in days |
| `CORS_ORIGIN` | Allowed CORS origin |
| `QUEUE_CONCURRENCY` | Worker concurrency (default: `5`) |

## 🔗 Frontend

The React frontend lives at [`https://github.com/alexfromural-lgtm/job-finder-react`](https://github.com/alexfromural-lgtm/job-finder-react) and proxies all `/api` calls to this service on port **5002**.

