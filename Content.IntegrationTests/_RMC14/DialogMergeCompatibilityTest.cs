using Content.Shared._RMC14.Dialog;
using Content.Shared._RMC14.Intel;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests._RMC14;

[TestFixture]
public sealed class DialogMergeCompatibilityTest
{
    [TestCase(false)]
    [TestCase(true)]
    public async Task OptionsKeepActorOwnershipAndResetCallbacks(bool enableSearch)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var target = entMan.SpawnEntity(null, map.GridCoords);
            var actor = entMan.SpawnEntity(null, map.GridCoords);
            var dialogs = server.System<DialogSystem>();

            dialogs.OpenConfirmation(target, actor, "Confirm", "Message", "callback");
            var dialog = entMan.GetComponent<DialogComponent>(actor);
            Assert.That(dialog.ConfirmEvent, Is.Not.Null);

            dialogs.OpenOptions(target, actor, "Options", [new DialogOption("First")], enableSearch: enableSearch);
            Assert.That(dialog.ConfirmEvent, Is.Null);

            var inputEvent = new IntelSafeCodeInputEvent(entMan.GetNetEntity(actor));
            dialogs.OpenInput(target, actor, "Input", inputEvent);
            Assert.That(dialog.InputEvent, Is.SameAs(inputEvent));
            dialogs.OpenOptions(target, actor, "Options", [new DialogOption("Second")], enableSearch: enableSearch);

            Assert.Multiple(() =>
            {
                Assert.That(dialog.Owner, Is.EqualTo(actor));
                Assert.That(dialog.EventTarget, Is.EqualTo(target));
                Assert.That(entMan.HasComponent<DialogComponent>(target), Is.False);
                Assert.That(dialog.DialogType, Is.EqualTo(DialogType.Options));
                Assert.That(dialog.EnableSearch, Is.EqualTo(enableSearch));
                Assert.That(dialog.InputEvent, Is.Null);
                Assert.That(dialog.ConfirmEvent, Is.Null);
                Assert.That(dialog.Options, Has.Count.EqualTo(1));
                Assert.That(dialog.Options[0].Text, Is.EqualTo("Second"));
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SelfTargetedOptionsForwardSearchSetting()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var actor = server.EntMan.SpawnEntity(null, map.GridCoords);
            var dialogs = server.System<DialogSystem>();
            dialogs.OpenOptions(actor, "Options", [new DialogOption("First")], enableSearch: false);
            var dialog = server.EntMan.GetComponent<DialogComponent>(actor);
            Assert.That(dialog.EventTarget, Is.EqualTo(actor));
            Assert.That(dialog.EnableSearch, Is.False);

            dialogs.OpenOptions(actor, "Options", [new DialogOption("Second")]);
            Assert.That(dialog.EventTarget, Is.EqualTo(actor));
            Assert.That(dialog.EnableSearch, Is.True);
        });

        await pair.CleanReturnAsync();
    }
}
