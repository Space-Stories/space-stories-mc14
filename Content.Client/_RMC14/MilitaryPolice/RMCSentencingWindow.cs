using System.Linq;
using Content.Client._RMC14.UserInterface.Crt;
using Content.Client.Guidebook;
using Content.Shared._RMC14.MilitaryPolice;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Client._RMC14.MilitaryPolice;

public sealed class RMCSentencingWindow : RMCPoliceWindow
{
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IEntitySystemManager _systems = default!;

    public event Action<RMCSentencingMsg>? Message;
    private readonly RMCCrtLineEdit _search = new();
    private readonly RMCCrtOptionButton _category = new() { IconTexturePath = RMCCrtIcons.FilterTexture };
    private readonly BoxContainer _catalogue = Vertical();
    private readonly RMCCrtLabel _sectionNotes = Text(string.Empty, RMCCrtTone.Muted);
    private readonly RMCCrtLabel _sentencingNotes = Text(string.Empty, RMCCrtTone.Muted);
    private readonly RMCCrtActionButton _new;
    private readonly RMCCrtActionButton _scrap;
    private readonly RMCCrtActionButton _export;
    private RMCPoliceUiData? _state;
    private readonly HashSet<string> _expandedLaws = new();

    public RMCSentencingWindow() : base("rmc-police-sentencing-title")
    {
        IoCManager.InjectDependencies(this);
        var header = Horizontal();
        header.AddChild(Heading("rmc-police-jas"));
        var guide = Button("rmc-police-open-law-guide", () =>
        {
            if (_state != null && _prototypes.TryIndex(_state.LawCatalogue, out var prototype) &&
                prototype.TryGetComponent(out RMCMarineLawCatalogueComponent? catalogue, ComponentFactory))
                _systems.GetEntitySystem<GuidebookSystem>().OpenHelp(new() { catalogue.Guide });
        });
        guide.HorizontalExpand = false;
        header.AddChild(guide);
        Toolbar.AddChild(header);
        var actions = Horizontal();
        _new = Button("rmc-police-new-report", () => Send(RMCSentencingAction.New), RMCCrtIcons.IdCard);
        _scrap = Button("rmc-police-scrap-report", () =>
        {
            if (_state == null)
                return;
            var revision = _state.Revision;
            Confirm("rmc-police-scrap-report", () =>
                Message?.Invoke(new RMCSentencingMsg(RMCSentencingAction.Scrap, revision)));
        }, RMCCrtIcons.Ban, true);
        _export = Button("rmc-police-export", () => Send(RMCSentencingAction.Export), RMCCrtIcons.PaperPlane);
        actions.AddChild(_new);
        actions.AddChild(_scrap);
        actions.AddChild(_export);
        Toolbar.AddChild(actions);
        Right.AddChild(Heading("rmc-police-law-catalogue"));
        _search.PlaceHolder = Loc.GetString("rmc-police-search-laws");
        _search.OnTextChanged += _ => PopulateCatalogue();
        Right.AddChild(_search);
        _category.OnItemSelected += _ => PopulateCatalogue();
        Right.AddChild(_category);
        Right.AddChild(_sectionNotes);
        Right.AddChild(_sentencingNotes);
        Right.AddChild(_catalogue);
    }

    public void Update(RMCPoliceUiData state)
    {
        if (_state == null || _state.LawCatalogue != state.LawCatalogue)
        {
            _category.Clear();
            _expandedLaws.Clear();
            if (_prototypes.TryIndex(state.LawCatalogue, out var prototype) &&
                prototype.TryGetComponent(out RMCMarineLawCatalogueComponent? catalogue, ComponentFactory))
            {
                foreach (var category in catalogue.Laws.Select(id => RMCMarineLaw.Get(_prototypes, ComponentFactory, id).Category)
                             .Distinct().OrderBy(category => category))
                    _category.AddItem(Loc.GetString($"rmc-police-law-category-{category}"), (int) category);
            }
        }
        _state = state;
        Connection.Text = state.Connected ? string.Empty : Loc.GetString("rmc-police-disconnected");
        Connection.Visible = !state.Connected;
        _new.Disabled = state.Report != null;
        _scrap.Disabled = state.Report == null;
        _export.Disabled = state.Report == null || string.IsNullOrWhiteSpace(state.Report.Suspect) ||
                           !state.Report.TryCalculateSentence(_prototypes, ComponentFactory, out _);
        PopulateReport();
        PopulateCatalogue();
    }

    private void Send(RMCSentencingAction action, string text = "", int index = 0)
    {
        if (_state != null)
            Message?.Invoke(new RMCSentencingMsg(action, _state.Revision, text, index));
    }

    private void EditReport(LocId title, string value, RMCSentencingAction action, int index = 0)
    {
        if (_state == null)
            return;
        var revision = _state.Revision;
        Edit(title, value, text => Message?.Invoke(new RMCSentencingMsg(action, revision, text, index)));
    }

    private void PopulateReport()
    {
        Left.DisposeAllChildren();
        if (_state?.Report is not { } report)
        {
            Left.AddChild(Heading("rmc-police-incident-report"));
            Left.AddChild(Text(Loc.GetString("rmc-police-report-help"), RMCCrtTone.Muted));
            return;
        }

        Left.AddChild(Heading("rmc-police-incident-report"));
        Left.AddChild(Text(Loc.GetString("rmc-police-suspect-value",
            ("name", string.IsNullOrWhiteSpace(report.Suspect)
                ? Loc.GetString("rmc-police-unknown") : report.Suspect))));
        Left.AddChild(Button("rmc-police-scan-suspect", () => Send(RMCSentencingAction.Suspect),
            RMCCrtIcons.IdCard));
        Left.AddChild(Text(Loc.GetString("rmc-police-sentence-value",
            ("sentence", Sentence(report, _prototypes, ComponentFactory))), RMCCrtTone.Warning));
        Left.AddChild(new RMCCrtSeparator { Margin = new Thickness(0, 6) });
        var summary = Horizontal();
        summary.AddChild(Heading("rmc-police-summary"));
        var editSummary = Button("rmc-police-edit", () =>
            EditReport("rmc-police-summary", report.Summary, RMCSentencingAction.Summary));
        editSummary.IconTexturePath = RMCCrtIcons.PencilTexture;
        editSummary.HorizontalExpand = false;
        editSummary.ToolTip = Loc.GetString("rmc-police-edit-summary");
        summary.AddChild(editSummary);
        Left.AddChild(summary);
        Left.AddChild(Text(string.IsNullOrWhiteSpace(report.Summary)
            ? Loc.GetString("rmc-police-none") : report.Summary));
        Left.AddChild(new RMCCrtSeparator { Margin = new Thickness(0, 6) });
        Left.AddChild(Heading("rmc-police-charges"));
        for (var i = 0; i < report.Charges.Count; i++)
        {
            var index = i;
            var charge = report.Charges[i];
            if (!RMCMarineLaw.TryGet(_prototypes, ComponentFactory, charge.Law, out var law))
                continue;
            var content = Vertical();
            content.Margin = new Thickness(0, 4);
            var header = Horizontal();
            header.AddChild(Text(law.Name));
            var remove = Button("rmc-police-remove-short", () =>
                Send(RMCSentencingAction.RemoveCharge, index: index),
                RMCCrtIcons.Ban, true);
            remove.HorizontalExpand = false;
            remove.ToolTip = Loc.GetString("rmc-police-remove-charge");
            header.AddChild(remove);
            content.AddChild(header);
            content.AddChild(Text(RMCMarineSentence.DescribePunishmentField(law.Punishment, law.BrigTime),
                RMCCrtTone.Warning));
            if (law.Punishment == RMCMarineLawPunishment.SameAsCrime &&
                _prototypes.TryIndex(report.LawCatalogue, out var prototype) &&
                prototype.TryGetComponent(out RMCMarineLawCatalogueComponent? catalogue, ComponentFactory))
            {
                var related = new RMCCrtOptionButton();
                related.AddItem(Loc.GetString("rmc-police-select-related-crime"), -1);
                var choices = catalogue.Laws.Select(id => RMCMarineLaw.Get(_prototypes, ComponentFactory, id))
                    .Where(candidate => candidate.Punishment != RMCMarineLawPunishment.SameAsCrime)
                    .OrderBy(candidate => candidate.Name).ToArray();
                for (var j = 0; j < choices.Length; j++)
                {
                    var choice = choices[j];
                    related.AddItem($"{choice.Name}: " +
                                    RMCMarineSentence.DescribePunishmentField(choice.Punishment, choice.BrigTime), j);
                    if (charge.RelatedCrime == choice.ID)
                        related.SelectId(j);
                }
                related.OnItemSelected += choice =>
                {
                    if (choice >= 0 && choice < choices.Length)
                        Send(RMCSentencingAction.RelatedCrime, choices[choice].ID, index);
                };
                content.AddChild(related);
            }
            if (law.SpecialPunishment is { } punishment)
                content.AddChild(Text(Loc.GetString(punishment), RMCCrtTone.Warning));
            if (law.Conditions is { } conditions)
                content.AddChild(Text(Loc.GetString(conditions), RMCCrtTone.Muted));
            Left.AddChild(content);
        }
        if (report.Charges.Count == 0)
            Left.AddChild(Text(Loc.GetString("rmc-police-no-charges"), RMCCrtTone.Muted));
        Entries(report.Witnesses, true);
        Entries(report.Evidence, false);
    }

    private void Entries(List<RMCIncidentEntry> entries, bool witnesses)
    {
        Left.AddChild(new RMCCrtSeparator { Margin = new Thickness(0, 6) });
        var header = Horizontal();
        header.AddChild(Heading(witnesses ? "rmc-police-witnesses" : "rmc-police-evidence"));
        var add = Button("rmc-police-register-short", () =>
            Send(witnesses ? RMCSentencingAction.AddWitness : RMCSentencingAction.AddEvidence),
            witnesses ? RMCCrtIcons.Users : RMCCrtIcons.Cog);
        add.HorizontalExpand = false;
        add.ToolTip = Loc.GetString(witnesses ? "rmc-police-add-witness" : "rmc-police-add-evidence");
        header.AddChild(add);
        Left.AddChild(header);
        for (var i = 0; i < entries.Count; i++)
        {
            var index = i;
            var entry = entries[i];
            var content = Vertical();
            content.Margin = new Thickness(0, 4);
            content.AddChild(Text(entry.Name));
            var row = Horizontal();
            row.HorizontalExpand = false;
            row.HorizontalAlignment = HAlignment.Right;
            var edit = Button("rmc-police-edit", () => EditReport("rmc-police-edit-notes", entry.Notes,
                witnesses ? RMCSentencingAction.EditWitness : RMCSentencingAction.EditEvidence, index));
            edit.IconTexturePath = RMCCrtIcons.PencilTexture;
            edit.HorizontalExpand = false;
            edit.ToolTip = Loc.GetString("rmc-police-edit-notes");
            row.AddChild(edit);
            var remove = Button("rmc-police-remove-short", () =>
                Send(witnesses ? RMCSentencingAction.RemoveWitness : RMCSentencingAction.RemoveEvidence, index: index),
                RMCCrtIcons.Ban, true);
            remove.HorizontalExpand = false;
            remove.ToolTip = Loc.GetString("rmc-police-remove-entry");
            row.AddChild(remove);
            content.AddChild(row);
            if (!string.IsNullOrWhiteSpace(entry.Notes))
                content.AddChild(Text(entry.Notes));
            Left.AddChild(content);
        }
    }

    private void PopulateCatalogue()
    {
        _catalogue.DisposeAllChildren();
        if (_state == null || !_prototypes.TryIndex(_state.LawCatalogue, out var prototype) ||
            !prototype.TryGetComponent(out RMCMarineLawCatalogueComponent? catalogue, ComponentFactory))
            return;
        var laws = catalogue.Laws.Select(id => RMCMarineLaw.Get(_prototypes, ComponentFactory, id))
            .Where(law => (int) law.Category == _category.SelectedId)
            .Where(law => $"{law.Name} {law.Description}"
                .Contains(_search.Text, StringComparison.OrdinalIgnoreCase))
            .OrderBy(law => law.Name);
        _sectionNotes.Text = catalogue.SectionNotes.TryGetValue((RMCMarineLawCategory) _category.SelectedId, out var note)
            ? Loc.GetString(note) : string.Empty;
        _sectionNotes.Visible = !string.IsNullOrEmpty(_sectionNotes.Text);
        _sentencingNotes.Text = Loc.GetString(catalogue.SentencingNotes);
        foreach (var law in laws)
        {
            var section = new RMCCrtCollapsibleSection
            {
                Title = law.Name,
                Expanded = _expandedLaws.Contains(law.ID),
            };
            section.Summary.Text = RMCMarineSentence.DescribePunishmentField(law.Punishment, law.BrigTime);
            section.Summary.Tone = RMCCrtTone.Warning;
            section.Summary.Visible = true;
            section.OnToggled += expanded =>
            {
                if (expanded)
                    _expandedLaws.Add(law.ID);
                else
                    _expandedLaws.Remove(law.ID);
            };
            LawDetails(section.Body, law);
            var add = Button("rmc-police-add-charge", () => Send(RMCSentencingAction.AddCharge, law.ID));
            add.Disabled = _state?.Report == null || _state.Report.Charges.Count >= RMCIncidentReport.MaxEntries;
            section.Body.AddChild(add);
            _catalogue.AddChild(section);
        }
        if (_catalogue.ChildCount == 0)
            _catalogue.AddChild(Text(Loc.GetString("rmc-police-no-laws"), RMCCrtTone.Muted));
    }

    private static void LawDetails(BoxContainer content, RMCMarineLaw law)
    {
        content.AddChild(Text(law.Description));
        if (law.SpecialPunishment is { } punishment)
            content.AddChild(Text(Loc.GetString(punishment), RMCCrtTone.Warning));
        if (law.Conditions is { } conditions)
            content.AddChild(Text(Loc.GetString(conditions), RMCCrtTone.Muted));
    }
}
