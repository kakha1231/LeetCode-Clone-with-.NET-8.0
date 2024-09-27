﻿using Microsoft.AspNetCore.Identity;

namespace informaticsge.Models;

public class User : IdentityUser
{
    public ICollection<Submissions>? Submissions { set; get; }
    
    }