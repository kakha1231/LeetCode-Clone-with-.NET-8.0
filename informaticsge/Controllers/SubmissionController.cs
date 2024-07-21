﻿using System.Security.Claims;
using informaticsge.Dto;
using informaticsge.entity;
using informaticsge.models;
using informaticsge.Models;
using informaticsge.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Microsoft.Extensions.Logging;

namespace informaticsge.Controllers;

[Route("api/[controller]")]
[ApiController]
public class SubmissionController : ControllerBase
{
    private readonly AppDBContext _appDbContext;
    private readonly HttpClient _httpClient;
    private readonly UserManager<User> _userManager;

    public SubmissionController(AppDBContext appDbContext, HttpClient httpClient, UserService userService, UserManager<User> userManager)
    {
        _appDbContext = appDbContext;
        _httpClient = httpClient;
        _userManager = userManager;
    }
    

    [HttpPost("/submit")]
    [Authorize]
    public async Task<IActionResult> Submit(int problemId, [FromBody]string userCode)
    {
        try
        {
            var submissionRequest = await PrepareSubmissionRequest(problemId, userCode);
            var submissionResponse = await CallCompilationApi(submissionRequest); 
            
            await ProcessCompilationResult(problemId, User, userCode, submissionResponse);
            
            return Ok(submissionResponse);
            
        }
        catch (HttpRequestException ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
        }
        catch (JsonSerializationException ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Unexpected error: {ex.Message}");
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
        }
    }



    private async Task<CompilationRequestDto> PrepareSubmissionRequest(int problemId, string userCode)
    {
        var problem = await _appDbContext.Problems.Include(pr => pr.TestCases).FirstOrDefaultAsync(problem => problem.Id == problemId);
    
        var testCaseDtos = problem.TestCases.Select(tc => new TestCaseDto
        {
            Input = tc.Input,
            ExpectedOutput = tc.ExpectedOutput
        }).ToList();

        return new CompilationRequestDto
        {
            Code = userCode,
            MemoryLimitMb = problem.MemoryLimit,
            TimeLimitMs = problem.RuntimeLimit,
            Testcases = testCaseDtos
        };
    }

    private async Task<List<SubmissionResultDTO>> CallCompilationApi(CompilationRequestDto compilationRequest)
    {
        var response = await _httpClient.PostAsJsonAsync("http://localhost:5144/compile", compilationRequest);

        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine($"API call failed with status code: {response.StatusCode}");
            throw new HttpRequestException($"API call failed with status code: {response.StatusCode}");
        }

        var content = await response.Content.ReadAsStringAsync();
        var compilationResponse = JsonConvert.DeserializeObject<List<SubmissionResultDTO>>(content);
        
        if (compilationResponse == null)
        {
            Console.WriteLine("Failed to deserialize API response");
            throw new JsonSerializationException("Failed to deserialize API response");
        }

        return compilationResponse;
    }


    private async Task ProcessCompilationResult(int problemId,ClaimsPrincipal user , string userCode, List<SubmissionResultDTO> results)
    {

        var problem = await _appDbContext.Problems.FirstOrDefaultAsync(pr => pr.Id == problemId);
        
        //checks if there is unsuccessful test and returns it. if there is no unsuccessful tests - returns first
        var checkForUnSuccessful = results.FirstOrDefault(r => r.Success == false);
        
        var submission = new Submissions
        {
            AuthUsername = user.Claims.First(u => u.Type == "UserName").Value,
            Code = userCode,
            ProblemId = problemId,
            ProblemName = problem.Name,
            Input = checkForUnSuccessful.Input,
            ExpectedOutput = checkForUnSuccessful.ExpectedOutput,
            Output = checkForUnSuccessful.Output,
            UserId = user.Claims.First(u => u.Type == "Id").Value
        };

        _appDbContext.Submissions.Add(submission);
        await _appDbContext.SaveChangesAsync();

    }


    
/*foreach (var result in results)
{
    if (!result.Success)
    {
        var submission = new Submissions
        {
            AuthUsername = user.Claims.First(u => u.Type == "UserName").Value,
            Code = userCode,
            ProblemId = problemId,
            ProblemName = problem.Name,
            Input = result.Input,
            ExpectedOutput = result.ExpectedOutput,
            Output = result.Output,
            UserId = user.Claims.First(u => u.Type == "Id").Value
        }; 
        return;
    }
    else
    {
        var submission = new Submissions
        {
            AuthUsername = user.Claims.First(u => u.Type == "UserName").Value,
            Code = userCode,
            ProblemId = problemId,
            ProblemName = problem.Name,
            Input = result.Input,
            ExpectedOutput = result.ExpectedOutput,
            Output = result.Output,
            UserId = user.Claims.First(u => u.Type == "Id").Value
        }; 
        return;
                
    }*/
    
}