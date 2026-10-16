# SSP1 – Weather Images

An app that generates images with current weather data for Dutch weather stations.
A single API call starts a background process: weather data is fetched from Buienradar,
and for each weather station a photo is taken from Unsplash, the weather data is written
on it, and the result is stored in Azure Blob Storage.

## How it works

```
POST /jobs
   │
   ▼
[queue: start-jobs] ──► fetch-weather-job
                            │  fetches Buienradar data
                            │  sends 1 message per station (max 50)
                            ▼
                     [queue: process-image] ──► process-image-job (1 run per station)
                                                   │  gets a photo from Unsplash
                                                   │  writes the weather data on it
                                                   ▼
                                            [blob container: images]
                                              {jobId}/{stationId}.png
                                                   │
                                                   ▼
                                     GET /jobs/{jobId}/images (SAS links)
```

| Project | What it does |
| --- | --- |
| `api` | Web API. `POST /jobs` returns a job id immediately (202 Accepted) and puts the job on the `start-jobs` queue. `GET /jobs/{jobId}/images` returns temporary SAS links to the finished images of that job. |
| `fetch-weather-job` | Reads one message from `start-jobs`, fetches weather data from Buienradar and fans out one message per station to `process-image`. |
| `process-image-job` | Reads one message from `process-image`, gets a photo from Unsplash (searched by the station's region), writes the weather data on it and uploads it to Blob Storage. |
| `Shared` | Models used by more than one project (`WeatherStation`, `ImageJobMessage`). |

Every message is only deleted from its queue **after** the work has succeeded.
If a job crashes, the message becomes visible again and can be retried.

## API endpoints

| Method | Endpoint | Description |
| --- | --- | --- |
| `POST` | `/jobs` | Starts a new job. Returns `202 Accepted` with the `jobId`. The work happens in the background. |
| `GET` | `/jobs/{jobId}/images` | Returns a link to every image of the job that is finished so far. |

All requests are documented in `api/api.http`.

### Image links with SAS tokens

The `images` blob container is **private**: the images cannot be opened with a plain URL.
Instead, `GET /jobs/{jobId}/images` creates a **SAS (Shared Access Signature) link** for every image:

- **Read-only** – the link can only be used to view the image
- **One blob only** – the link works for that single image, not the whole container
- **Valid for 1 hour** – after that the link stops working

Only blobs whose name starts with `{jobId}/` are returned, so every job only sees its own images.

The endpoint returns the images that are finished **so far**, so calling it while the job is still running gives a partial list.

## Setup

- .NET 10 SDK
- [Azurite](https://learn.microsoft.com/en-us/azure/storage/common/storage-use-azurite) (VS Code extension)
- An Unsplash developer account

## Local testing

All storage parts of this project were developed and tested locally before deploying to Azure, using:

- **Azurite** (VS Code extension) – local emulator for Azure Storage (Blob, Queue and Table)
- **Microsoft Azure Storage Explorer** – to inspect queues, messages and blobs

What was tested locally:

- **Queue `start-jobs`** – `POST /jobs` creates a message with the job id; `fetch-weather-job` receives it and deletes it after processing.
- **Queue `process-image`** – `fetch-weather-job` creates one message per weather station (fan-out); `process-image-job` receives one message, processes it and deletes it.
- **Blob container `images`** – `process-image-job` uploads the generated image as `{jobId}/{stationId}.png`, viewable in Storage Explorer.
- **SAS links** – `GET /jobs/{jobId}/images` returns a read-only link per image, valid for 1 hour. The same URL without the SAS token is refused with `AuthorizationFailure`, because the `images` container is private.
- **Table `jobs`** – the API creates the job row (`Queued`), fetch-weather-job sets `Processing` and `TotalImages`, and process-image-job adds one row per finished image. `GET /jobs/{jobId}` shows the progress (for example 3 of 40, 7%).

Screenshots of these:

![Messages in start-jobs queue](docs/screenshots/start-jobs-queue.png)
![Messages in process-image queue](docs/screenshots/process-image-queue.png)
![Blob storage](docs/screenshots/blob-image.png)
![Table storage](docs/screenshots/table-image.png)

## Notes

### Station count

The assignment brief references "50 weather stations," but Buienradar's live feed does not return a fixed number.
The actual count fluctuates (individual stations can be offline for maintenance, etc.).
Observed live responses have ranged from around 40 to 43 stations rather than exactly 50.
The application therefore uses **at most 50** stations and processes however many the feed returns.

### ImageSharp version

`SixLabors.ImageSharp` **3.1.12** and `SixLabors.ImageSharp.Drawing` **2.1.7** are pinned on purpose.
These are the same versions as the sample in `InHolland-CCD/2026/samples/ImageEditor`.

When building, NuGet shows a warning that ImageSharp 3.1.12 has known security advisories.
This was investigated:

- **All 3.x versions** of ImageSharp (including the newer 3.2.0) are flagged with moderate and high severity advisories.
- Only the **4.x line** (4.1.2 and later) has the fixes, but together with ImageSharp.Drawing 3.x it:
  - requires a **commercial Six Labors license key** for Release builds (needed for the Docker image), and
  - has **breaking API changes** (`DrawText` no longer works as used in `ImageHelper.cs`).

Upgrading was tried, but it broke the build, so it was rolled back to the versions above.

**Why the risk is low:** the advisories are about processing malicious image files.
This app only processes photos downloaded from the Unsplash API (a trusted source), never images uploaded by users.

## Course material used

- `ImageHelper.cs` is based on the course sample
  ([wearetriple/InHolland-CCD/2026/samples/ImageEditor](https://github.com/wearetriple/InHolland-CCD/tree/main/2026/samples/ImageEditor)),
  extended with a semi-transparent dark background behind each line of text for readability.
- The SAS endpoint is based on `ImagesController` from the course demo
  (`InHolland-CCD/2026/demos/src/DemoImagesApi`), adapted to return only the images of one job.

## AI usage

**Claude (Opus 5.5)** was used during this assignment, inside a Claude project that contained the assignment brief and the course slides.

How it was used:

- suggesting code, which I added to the project myself.
- explaining what each piece of code does.
- splitting the assignment into small steps.
- understanding errors.

How I worked with it:

- I did not give the assignment to the AI and let it build everything at once, I asked to work in small steps instead.
- Every step was run and tested by me locally before moving on to the next one.
- I reviewed the code in each step and made my own choices along the way.


### Job status in Table storage

All status data is stored in one table, `jobs`, using the **jobId as PartitionKey**:

| RowKey | Columns | Written by |
| --- | --- | --- |
| `job` | `Status`, `TotalImages`, `CreatedAt` | API (`Queued`), fetch-weather-job (`Processing` + total) |
| `image-{stationId}` | `StationName`, `BlobName`, `FinishedAt` | process-image-job, one row per finished image |

Each image gets its own row instead of incrementing a counter on the job row, because many
image jobs run at the same time and would overwrite each other's count.
`GET /jobs/{jobId}` counts the image rows to calculate progress, and reports `Completed`
when the number of finished images reaches `TotalImages`.