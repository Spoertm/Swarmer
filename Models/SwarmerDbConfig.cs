﻿using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace Swarmer.Models;

[Keyless]
public class SwarmerDbConfig
{
	[Column(TypeName = "jsonb")]
	public string JsonConfig { get; set; } = null!;
}
