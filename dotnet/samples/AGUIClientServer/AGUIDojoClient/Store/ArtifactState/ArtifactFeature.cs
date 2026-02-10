// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Immutable;
using Fluxor;

namespace AGUIDojoClient.Store.ArtifactState;

/// <summary>
/// Fluxor feature that provides the initial <see cref="ArtifactState"/> and registers
/// it with the Fluxor store via assembly scanning.
/// </summary>
/// <remarks>
/// The initial state has no artifacts, no visible tabs, document preview enabled,
/// and <see cref="ArtifactType.None"/> as the active artifact.
/// Fluxor discovers this feature automatically through
/// <c>AddFluxor(o =&gt; o.ScanAssemblies(typeof(Program).Assembly))</c>
/// registered in <c>Program.cs</c>.
/// </remarks>
public class ArtifactFeature : Feature<ArtifactState>
{
    /// <inheritdoc />
    public override string GetName() => "Artifact";

    /// <inheritdoc />
    protected override ArtifactState GetInitialState() =>
        new(
            CurrentRecipe: null,
            CurrentDocumentState: null,
            IsDocumentPreview: true,
            HasInteractiveArtifact: false,
            ActiveArtifactType: ArtifactType.None,
            DiffPreview: null,
            CurrentDataGrid: null,
            VisibleTabs: ImmutableHashSet<ArtifactType>.Empty);
}
