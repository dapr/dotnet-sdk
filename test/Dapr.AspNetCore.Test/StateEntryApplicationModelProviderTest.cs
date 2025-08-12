// ------------------------------------------------------------------------
// Copyright 2021 The Dapr Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//     http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ------------------------------------------------------------------------

namespace Dapr.AspNetCore.Test;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Dapr.AspNetCore.Resources;
using Shouldly;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Xunit;

public class StateEntryApplicationModelProviderTest
{
    [Fact]
    public void OnProvidersExecuted_NullActionsBindingSource()
    {
        var provider = new StateEntryApplicationModelProvider();
        var context = CreateContext(nameof(ApplicationModelProviderTestController.Get));

        Action action = () => provider.OnProvidersExecuted(context);

        action.ShouldNotThrow();
    }

    [Fact]
    public void OnProvidersExecuted_StateEntryParameterThrows()
    {
        var provider = new StateEntryApplicationModelProvider();
        var context = CreateContext(nameof(ApplicationModelProviderTestController.Post));

        Action action = () => provider.OnProvidersExecuted(context);

        action
            .ShouldThrow<InvalidOperationException>(SR.ErrorStateStoreNameNotProvidedForStateEntry);
    }

    private ApplicationModelProviderContext CreateContext(string methodName)
    {
        var controllerType = typeof(ApplicationModelProviderTestController).GetTypeInfo();
        var typeInfoList = new List<TypeInfo> { controllerType };

        var context = new ApplicationModelProviderContext(typeInfoList);
        var controllerModel = new ControllerModel(controllerType, new List<object>(0));

        context.Result.Controllers.Add(controllerModel);

        var methodInfo = controllerType.AsType().GetMethods().First(m => m.Name.Equals(methodName));
        var actionModel = new ActionModel(methodInfo, controllerModel.Attributes)
        {
            Controller = controllerModel
        };

        controllerModel.Actions.Add(actionModel);
        var parameterInfo = actionModel.ActionMethod.GetParameters().First();
        var parameterModel = new ParameterModel(parameterInfo, controllerModel.Attributes)
        {
            BindingInfo = new BindingInfo(),
            Action = actionModel,
        };

        actionModel.Parameters.Add(parameterModel);

        return context;
    }

    [Controller]
    private class ApplicationModelProviderTestController : Controller
    {
        [HttpGet]
        public void Get([Bind(Prefix = "s")]int someId) { }

        [HttpPost]
        public void Post(StateEntry<Subscription> bogusEntry) { }
    }
}