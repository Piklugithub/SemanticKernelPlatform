using SemanticKernelPlatform.Application.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace SemanticKernelPlatform.Application.Interfaces
{
    public interface IChatService
    {
        Task<ChatResponse> SendMessageAsync(
            ChatRequest request,
            CancellationToken cancellationToken = default);
    }
}
