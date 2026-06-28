# Job Finder — .NET 8 C# Backend

A production-ready ASP.NET Core 8 Web API that is a **drop-in replacement** for the Node.js backend. It connects to the same React frontend with zero frontend configuration changes.

## Tech Stack

| Component | Technology |
|-----------|-----------|
| Runtime | .NET 8 / ASP.NET Core |
| Database | PostgreSQL 16 via Npgsql + EF Core 8 |
| Queue | Redis 7 + Custom `IHostedService` worker |
| Auth | JWT (HMAC-SHA256) via HTTP-only cookies |
| Validation | FluentValidation 11 |
| Containerization | Docker + Docker Compose |

## Architecture

```
job-finder-react (port 3000)
    └── Vite proxy /api → localhost:5002
            └── job-finder-backend-csharp (port 5002)
                    ├── PostgreSQL (port 5432)
                    └── Redis (port 6379)
                            └── QueueWorker (IHostedService)
```

## Running with Docker (recommended)

> ⚠️ **Only one backend should run at a time.** Both the Node.js and C# stacks use the same ports and Docker network.

### 1. Create the shared Docker network (once, shared with Node.js stack)

```bash
docker network create job-finder-network
```

### 2. Stop the Node.js backend if running

```bash
# In the job-finder-backend-node directory:
docker compose down
```

### 3. Start the C# backend

```bash
# In this directory:
docker compose up --build
```

### 4. Seed the database (first run only)

```bash
docker compose exec backend dotnet JobFinder.Api.dll --seed
```

This creates demo users:
| Email | Password | Role |
|-------|----------|------|
| `admin@example1.com` | `admin` | ADMIN |
| `recruiter@example.com` | `recruiter123` | RECRUITER |
| `seeker@example.com` | `seeker123` | JOB_SEEKER |


The React frontend (`job-finder-react`) can now be started without any changes:

```bash
# In job-finder-react:
npm run dev
```

## Running Locally (without Docker)

Requires a running PostgreSQL on port `5432` and Redis on port `6379`.

```bash
cd src/JobFinder.Api
dotnet run
```

To seed locally:

```bash
dotnet run -- --seed
```

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
