namespace Shared.Models;

// The possible statuses of a job
public enum JobStatus
{
    Queued,      // set by the API when the job is created
    Processing,  // set by fetch-weather-job after fetching the stations
    Completed    // set when all images have been processed
}
