using System.Linq;
using Sources.Models;
using Sources.ViewModels;
using Xunit;

namespace Sources.Tests;

public class HelpViewModelTests
{
    [Fact]
    public void Topics_IncludesTheThreeNewRound160Topics_AndTotalIsFifteen()
    {
        var vm = new HelpViewModel();

        Assert.Equal(15, vm.Topics.Count);
        Assert.Contains(vm.Topics, t => t.Id == "LeakTests");
        Assert.Contains(vm.Topics, t => t.Id == "Deletions");
        Assert.Contains(vm.Topics, t => t.Id == "NeutronSources");
    }

    [Fact]
    public void NewTopics_HaveExpectedTargetViewNames()
    {
        var vm = new HelpViewModel();

        var leakTests = vm.Topics.Single(t => t.Id == "LeakTests");
        var deletions = vm.Topics.Single(t => t.Id == "Deletions");
        var neutronSources = vm.Topics.Single(t => t.Id == "NeutronSources");

        Assert.Equal("LeakTests", leakTests.TargetViewName);
        Assert.Equal("Deletions", deletions.TargetViewName);
        Assert.Equal("Sources", neutronSources.TargetViewName);
    }

    [Fact]
    public void RoleFilter_SafetyOfficer_StillShowsNewTopics()
    {
        var vm = new HelpViewModel();
        var safetyOfficerOption = vm.RoleFilterOptions.Single(o => o.RoleKey == HelpRoles.SafetyOfficer);

        vm.SelectedRoleFilter = safetyOfficerOption;

        Assert.Contains(vm.FilteredTopics, t => t.Id == "LeakTests");
        Assert.Contains(vm.FilteredTopics, t => t.Id == "Deletions");
        Assert.Contains(vm.FilteredTopics, t => t.Id == "NeutronSources");
    }

    [Fact]
    public void NewTopics_DoNotDeclareStorekeeperRole()
    {
        // Per the round contract, the 3 new topics are scoped to { All, SafetyOfficer } only.
        // Note: HelpViewModel.ApplyFilter() ORs in `t.Roles.Contains(HelpRoles.All)`, and every
        // topic (old and new) always includes HelpRoles.All, so selecting the Storekeeper filter
        // does not actually remove any topic from FilteredTopics in the current implementation.
        // That filtering behavior is pre-existing and out of this round's scope, so this test
        // verifies the underlying Roles data directly instead of the (currently no-op) UI filter.
        var vm = new HelpViewModel();

        var leakTests = vm.Topics.Single(t => t.Id == "LeakTests");
        var deletions = vm.Topics.Single(t => t.Id == "Deletions");
        var neutronSources = vm.Topics.Single(t => t.Id == "NeutronSources");

        Assert.DoesNotContain(HelpRoles.Storekeeper, leakTests.Roles);
        Assert.DoesNotContain(HelpRoles.Storekeeper, deletions.Roles);
        Assert.DoesNotContain(HelpRoles.Storekeeper, neutronSources.Roles);
    }

    [Fact]
    public void Search_ByLeakTestsKeyword_FindsLeakTestsTopic()
    {
        var vm = new HelpViewModel();

        vm.SearchText = "wipe test";

        Assert.Contains(vm.FilteredTopics, t => t.Id == "LeakTests");
    }

    [Fact]
    public void Search_ByDeletionsKeyword_FindsDeletionsTopic()
    {
        var vm = new HelpViewModel();

        vm.SearchText = "soft delete";

        Assert.Contains(vm.FilteredTopics, t => t.Id == "Deletions");
    }

    [Fact]
    public void Search_ByNeutronSourcesKeyword_FindsNeutronSourcesTopic()
    {
        var vm = new HelpViewModel();

        vm.SearchText = "emission rate";

        Assert.Contains(vm.FilteredTopics, t => t.Id == "NeutronSources");
    }
}
