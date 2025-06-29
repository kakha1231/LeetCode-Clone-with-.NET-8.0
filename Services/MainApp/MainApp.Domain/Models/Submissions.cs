﻿using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MainApp.Domain.Models
{
    public class Submissions
    {
        [Key]
        public int Id { get; set; }
        public string AuthUsername { get; set; }
        
        public string Language { set; get; }
        public string Code { get; set; }
        public int ProblemId { get; set; }
        public string ProblemName { get; set; }
        public string? Status { get; set; }
        public string? Input { set; get; }
        public string? ExpectedOutput { set; get; }
        public string? Output { set; get; }
        public string UserId { get; set; }
        
        [ForeignKey(nameof(UserId))]
        public User User { get; set; }
    }
}