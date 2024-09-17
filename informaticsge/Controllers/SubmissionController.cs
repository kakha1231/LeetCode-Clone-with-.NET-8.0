﻿using System.Security.Claims;
using informaticsge.Dto.Request;
using informaticsge.Dto.Response;
using informaticsge.Entity;
using informaticsge.models;
using informaticsge.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;


namespace informaticsge.Controllers;

[Route("api/[controller]")]
[ApiController]
public class SubmissionController : ControllerBase
{
    private readonly AppDbContext _appDbContext;
    private readonly HttpClient _httpClient;
    private readonly ILogger<SubmissionController> _logger;

    public SubmissionController(AppDbContext appDbContext, HttpClient httpClient,  ILogger<SubmissionController> logger)
    {
        _appDbContext = appDbContext;
        _httpClient = httpClient;
        _logger = logger;
    }
    

    [HttpPost("/submit")]
    [Authorize(AuthenticationSchemes = "Bearer")]
    public async Task<IActionResult> Submit(SubmissionDto submissionDto,[FromBody]string userCode) //for now i need my code plain text not json
    {
        var username = User.Claims.First(u => u.Type == "UserName").Value;
        
            _logger.LogInformation("Received submission request for problem Id: {ProblemId} from User: {Username}. Code: {UserCode}",
            submissionDto.ProblemId, username, userCode);

            try
            {
                var submissionRequest = await PrepareSubmissionRequest(submissionDto, userCode);

                var submissionResponse = await CallCompilationApi(submissionRequest);

                await ProcessSubmissionResult(submissionDto, User, userCode, submissionResponse);

                return Ok(submissionResponse);
            }
            catch(Exception ex)
            {
                _logger.LogError( "submission request for problem Id: {ProblemId} from User: {Username} failed. ",submissionDto.ProblemId,username);
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
    }



    private async Task<SubmissionRequestDto> PrepareSubmissionRequest(SubmissionDto submissionDto, string userCode)
    {
        _logger.LogInformation("preparing submission Request");

        try
        {

            var problem = await _appDbContext.Problems.Include(pr => pr.TestCases)
                .FirstOrDefaultAsync(problem => problem.Id == submissionDto.ProblemId);

            if (problem == null)
            {
                _logger.LogWarning("Problem with Id {id} not found", submissionDto.ProblemId);

                throw new InvalidOperationException("problem not found");
            }

            var testCaseDtoList = problem.TestCases.Select(tc => new TestCaseDto
            {
                Input = tc.Input,
                ExpectedOutput = tc.ExpectedOutput
            }).ToList();

            return new SubmissionRequestDto
            {
                Language = submissionDto.Language,
                Code = userCode,
                MemoryLimitMb = problem.MemoryLimit,
                TimeLimitMs = problem.RuntimeLimit,
                TestCases = testCaseDtoList
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error preparing submission request for problem {id}.",submissionDto.ProblemId);
            throw;
        }

    }

    private async Task<List<SubmissionResultDto>> CallCompilationApi(SubmissionRequestDto submissionRequestDto)
    {
        _logger.LogInformation("calling compilation-API");
     
        try
        {
            var response = await _httpClient.PostAsJsonAsync("http://localhost:5144/submit", submissionRequestDto);
            
            var content = await response.Content.ReadAsStringAsync();
            var compilationResponse = JsonConvert.DeserializeObject<List<SubmissionResultDto>>(content);
            
            if (compilationResponse == null)
            { 
                _logger.LogError("Failed to deserialize API response");
                
                throw new JsonSerializationException("Failed to deserialize API response");
            }
            return compilationResponse;
        }
        catch (Exception ex)
        {
            _logger.LogCritical( ex.Message);
            
            throw new HttpRequestException(" Failed To Connect Submission-API Try Again later");
        }
    }


    private async Task ProcessSubmissionResult(SubmissionDto submissionDto,ClaimsPrincipal user, string userCode, List<SubmissionResultDto> results)
    {
        _logger.LogInformation("starting processing submission result");

        try
        {
            var problem = await _appDbContext.Problems.FirstOrDefaultAsync(pr => pr.Id == submissionDto.ProblemId);
            
            if (problem == null)
            {
                _logger.LogWarning("Problem with Id {id} not found", submissionDto.ProblemId);

                throw new InvalidOperationException("problem not found");
            }
        
            var checkForUnSuccessful = results.FirstOrDefault(r => r.Success == false) ?? results.FirstOrDefault();
        
            var submission = new Submissions
            {
                AuthUsername = user.Claims.First(u => u.Type == "UserName").Value,
                Code = userCode,
                ProblemId = submissionDto.ProblemId,
                ProblemName = problem.Name,
                Status = checkForUnSuccessful?.Status,
                Language = submissionDto.Language,
                Input = checkForUnSuccessful?.Input,
                ExpectedOutput = checkForUnSuccessful?.ExpectedOutput,
                Output = checkForUnSuccessful?.Output,
                UserId = user.Claims.First(u => u.Type == "Id").Value,
            };
            
            _appDbContext.Submissions.Add(submission);
            await _appDbContext.SaveChangesAsync();
            
            _logger.LogInformation("Submission result successfully added in database");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing submission result.");
            throw;
        }

    }
    
}