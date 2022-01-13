using System;
using System.Collections.Generic;

namespace Dapr.Client
{
    /// <summary>
    /// Class representing the response from a GetConfiguration API call.
    /// </summary>
    public class GetConfigurationResponse
    {
        private readonly IReadOnlyList<ConfigurationItem> items;

        /// <summary>
        /// Constructor for a GetConfigurationResponse.
        /// </summary>
        /// <param name="items">The items that were returned in the GetConfiguration call.</param>
        public GetConfigurationResponse(IReadOnlyList<ConfigurationItem> items)
        {
            this.items = items;
        }

        /// <summary>
        /// The items returned in a GetConfiguration call. <see cref="ConfigurationItem"/>
        /// </summary>
        public IReadOnlyList<ConfigurationItem> Items
        {
            get
            {
                return items;
            }
        }
    }
}
