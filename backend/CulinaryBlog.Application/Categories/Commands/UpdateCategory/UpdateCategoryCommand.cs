using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using CulinaryBlog.Application.Categories.DTOs;
using CulinaryBlog.Application.Common.Interfaces;
using MediatR;

namespace CulinaryBlog.Application.Categories.Commands.UpdateCategory;

public record UpdateCategoryCommand(
    [property: JsonIgnore] Guid Id,
    string Name,
    string? Description
) : IRequest<CategoryDto>, ICacheInvalidator
{
    // D8: Xóa cả 2 cache tag "categories" và "recipes"
    [JsonIgnore]
public IReadOnlyList<string> TagsToInvalidate => new[] { "categories", "recipes" };}