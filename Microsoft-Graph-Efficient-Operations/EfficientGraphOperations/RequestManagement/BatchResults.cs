using Microsoft.Graph;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EfficientGraphOperations.RequestManagement
{
    class BatchResults<TResponse>
    {
        public BatchResults(ICollection<TResponse> successfulResponses, ICollection<ErrorResponse> errorResponses)
        {
            this.SuccessfulResponses = successfulResponses;
            this.ErrorResponses = errorResponses;
        }
        public ICollection<TResponse> SuccessfulResponses { get; private set; }
        public ICollection<ErrorResponse> ErrorResponses { get; private set; }
        public bool HasErrors { get => ErrorResponses.Count > 0; }
    }
}
