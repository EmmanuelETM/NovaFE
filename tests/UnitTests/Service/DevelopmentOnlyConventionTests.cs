using System.Reflection;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using NovaFE.Service.Common;

namespace NovaFE.UnitTests.Service;

public class DevelopmentOnlyConventionTests
{
    [Fact]
    public void Strips_controllers_marked_development_only_and_keeps_the_rest()
    {
        var application = new ApplicationModel();
        application.Controllers.Add(new ControllerModel(
            typeof(DevOnlyController).GetTypeInfo(), [new DevelopmentOnlyAttribute()]));
        application.Controllers.Add(new ControllerModel(
            typeof(NormalController).GetTypeInfo(), []));

        new RemoveDevelopmentOnlyConvention().Apply(application);

        application.Controllers
            .Select(controller => controller.ControllerType)
            .ShouldBe([typeof(NormalController).GetTypeInfo()]);
    }

    private sealed class DevOnlyController;

    private sealed class NormalController;
}
