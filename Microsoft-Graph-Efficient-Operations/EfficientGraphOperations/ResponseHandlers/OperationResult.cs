using Microsoft.Graph;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EfficientRequestHandling.ResponseHandlers
{
    public class OperationResult<TEntity>
    {
        public OperationResult(TEntity item, ErrorResponse error, Uri requestUri)
        {
            Item = item;
            ErrorDetails = error;
            RequestUri = requestUri;
        }
        public TEntity Item { get; }
        public ErrorResponse ErrorDetails { get; }
        public Uri RequestUri { get; }

        public bool IsSuccessful { get { return this.ErrorDetails == default(ErrorResponse); } }
    }
}
