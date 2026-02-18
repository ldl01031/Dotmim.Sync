// ***********************************
// Dotmim.Sync.Web.Client
// HttpMessage.cs
// Modified: 02/17/2026 (Larry Leach)
// ***********************************

using Dotmim.Sync.Batch;
using Dotmim.Sync.Enumerations;
using Dotmim.Sync.Web.Client.BackwardCompatibility;
using System;
using System.Runtime.Serialization;

namespace Dotmim.Sync.Web.Client
{
    public interface IScopeMessage
    {
        SyncContext SyncContext { get; set; }
    }

    [DataContract(Name = "changesres"), Serializable]
    public class HttpMessageSendChangesResponse : IScopeMessage
    {
        public HttpMessageSendChangesResponse() { }

        public HttpMessageSendChangesResponse(SyncContext context)
        {
            this.SyncContext = context ?? throw new ArgumentNullException(nameof(context));
        }

        [DataMember(Name = "bc", IsRequired = false, Order = 4)]
        public int BatchCount { get; set; }

        [DataMember(Name = "bi", IsRequired = true, Order = 3)]
        public int BatchIndex { get; set; }

        [DataMember(Name = "changes", IsRequired = true, Order = 7)]
        public ContainerSet
            Changes { get; set; } // BE CAREFUL: If changes the order, change it too in "ContainerSetBoilerPlate" !

        [DataMember(Name = "cca", IsRequired = true, Order = 9)]
        public DatabaseChangesApplied ClientChangesApplied { get; set; }

        [DataMember(Name = "policy", IsRequired = true, Order = 10)]
        public ConflictResolutionPolicy ConflictResolutionPolicy { get; set; }

        [DataMember(Name = "islb", IsRequired = true, Order = 5)]
        public bool IsLastBatch { get; set; }

        [DataMember(Name = "rct", IsRequired = true, Order = 6)]
        public long RemoteClientTimestamp { get; set; }

        [DataMember(Name = "scs", IsRequired = true, Order = 8)]
        public DatabaseChangesSelected ServerChangesSelected { get; set; }

        [DataMember(Name = "ss", IsRequired = true, Order = 1)]
        public HttpStep ServerStep { get; set; }

        [DataMember(Name = "sc", IsRequired = true, Order = 2)]
        public SyncContext SyncContext { get; set; }
    }

    [DataContract(Name = "morechangesreq"), Serializable]
    public class HttpMessageGetMoreChangesRequest : IScopeMessage
    {
        public HttpMessageGetMoreChangesRequest() { }

        public HttpMessageGetMoreChangesRequest(SyncContext context, int batchIndexRequested)
        {
            this.BatchIndexRequested = batchIndexRequested;
            this.SyncContext = context ?? throw new ArgumentNullException(nameof(context));
        }

        [DataMember(Name = "bireq", IsRequired = true, Order = 2)]
        public int BatchIndexRequested { get; set; }

        [DataMember(Name = "sc", IsRequired = true, Order = 1)]
        public SyncContext SyncContext { get; set; }
    }

    [DataContract(Name = "changesreq"), Serializable]
    public class HttpMessageSendChangesRequest : IScopeMessage
    {
        public HttpMessageSendChangesRequest()
        {
        }

        public HttpMessageSendChangesRequest(SyncContext context, ScopeInfoClient cScopeInfoClient)
        {
            this.SyncContext = context;
            this.ScopeInfoClient = cScopeInfoClient;
            this.IsLastBatch = true;
            this.BatchCount = 0;
            this.BatchIndex = 0;
            this.Changes = new ContainerSet();
        }

        [DataMember(Name = "bc", IsRequired = false, Order = 4)]
        public int BatchCount { get; set; }

        [DataMember(Name = "bi", IsRequired = true, Order = 3)]
        public int BatchIndex { get; set; }

        [DataMember(Name = "changes", IsRequired = true, Order = 6)]
        public ContainerSet Changes { get; set; }

        [DataMember(Name = "clst", IsRequired = false, Order = 7)] // IsRequired = false to preserve backward compat
        public long ClientLastSyncTimestamp { get; set; }

        [DataMember(Name = "islb", IsRequired = true, Order = 5)]
        public bool IsLastBatch { get; set; }

        [DataMember(Name = "scope", IsRequired = false, Order = 8)] // IsRequired = false to preserve backward compat
        public OldScopeInfo OldScopeInfo { get; set; }

        [DataMember(Name = "scopeclient", IsRequired = false, Order = 2)]
        public ScopeInfoClient ScopeInfoClient { get; set; }

        [DataMember(Name = "sc", IsRequired = true, Order = 1)]
        public SyncContext SyncContext { get; set; }
    }

    [DataContract(Name = "ensureschemares"), Serializable]
    public class HttpMessageEnsureSchemaResponse : IScopeMessage
    {
        public HttpMessageEnsureSchemaResponse()
        {
        }

        public HttpMessageEnsureSchemaResponse(SyncContext context, ScopeInfo sScopeInfo)
        {
            this.SyncContext = context ?? throw new ArgumentNullException(nameof(context));
            this.ServerScopeInfo = sScopeInfo ?? throw new ArgumentNullException(nameof(sScopeInfo));
            this.Schema = sScopeInfo.Schema;
        }

        [DataMember(Name = "schema", IsRequired = true, Order = 2)]
        public SyncSet Schema { get; set; }

        [DataMember(Name = "ssi", IsRequired = true, Order = 3)]
        public ScopeInfo ServerScopeInfo { get; set; }

        [DataMember(Name = "sc", IsRequired = true, Order = 1)]
        public SyncContext SyncContext { get; set; }
    }

    [DataContract(Name = "ensurescopesres"), Serializable]
    public class HttpMessageEnsureScopesResponse : IScopeMessage
    {
        public HttpMessageEnsureScopesResponse()
        {
        }

        public HttpMessageEnsureScopesResponse(SyncContext context, ScopeInfo sScopeInfo)
        {
            this.SyncContext = context ?? throw new ArgumentNullException(nameof(context));
            this.ServerScopeInfo = sScopeInfo ?? throw new ArgumentNullException(nameof(sScopeInfo));
        }

        [DataMember(Name = "serverscope", IsRequired = true, Order = 2)]
        public ScopeInfo ServerScopeInfo { get; set; }

        [DataMember(Name = "sc", IsRequired = true, Order = 1)]
        public SyncContext SyncContext { get; set; }
    }

    [DataContract(Name = "ensurereq"), Serializable]
    public class HttpMessageEnsureScopesRequest : IScopeMessage
    {
        public HttpMessageEnsureScopesRequest() { }

        internal HttpMessageEnsureScopesRequest(SyncContext context)
        {
            this.SyncContext = context ?? throw new ArgumentNullException(nameof(context));
        }

        [DataMember(Name = "sc", IsRequired = true, Order = 1)]
        public SyncContext SyncContext { get; set; }
    }

    [DataContract(Name = "opreq"), Serializable]
    public class HttpMessageOperationRequest : IScopeMessage
    {
        public HttpMessageOperationRequest() { }

        public HttpMessageOperationRequest(SyncContext context, ScopeInfo cScopeInfo, ScopeInfoClient cScopeInfoClient)
        {
            this.SyncContext = context ?? throw new ArgumentNullException(nameof(context));
            this.ScopeInfoFromClient = cScopeInfo;
            this.ScopeInfoClient = cScopeInfoClient;
        }

        [DataMember(Name = "scopeclient", IsRequired = true, Order = 3)]
        public ScopeInfoClient ScopeInfoClient { get; set; }

        [DataMember(Name = "scope", IsRequired = true, Order = 2)]
        public ScopeInfo ScopeInfoFromClient { get; set; }

        [DataMember(Name = "sc", IsRequired = true, Order = 1)]
        public SyncContext SyncContext { get; set; }
    }

    [DataContract(Name = "opres"), Serializable]
    public class HttpMessageOperationResponse : IScopeMessage
    {
        public HttpMessageOperationResponse() { }

        public HttpMessageOperationResponse(SyncContext context, SyncOperation syncOperation)
        {
            this.SyncContext = context ?? throw new ArgumentNullException(nameof(context));
            this.SyncOperation = syncOperation;
        }

        [DataMember(Name = "so", IsRequired = true, Order = 2)]
        public SyncOperation SyncOperation { get; set; }

        [DataMember(Name = "sc", IsRequired = true, Order = 1)]
        public SyncContext SyncContext { get; set; }
    }

    [DataContract(Name = "remotetsres"), Serializable]
    public class HttpMessageRemoteTimestampResponse : IScopeMessage
    {
        public HttpMessageRemoteTimestampResponse()
        {
        }

        public HttpMessageRemoteTimestampResponse(SyncContext context, long remoteClientTimestamp)
        {
            this.SyncContext = context ?? throw new ArgumentNullException(nameof(context));
            this.RemoteClientTimestamp = remoteClientTimestamp;
        }

        [DataMember(Name = "rct", IsRequired = true, Order = 2)]
        public long RemoteClientTimestamp { get; set; }

        [DataMember(Name = "sc", IsRequired = true, Order = 1)]
        public SyncContext SyncContext { get; set; }
    }

    [DataContract(Name = "remotetsreq"), Serializable]
    public class HttpMessageRemoteTimestampRequest : IScopeMessage
    {
        public HttpMessageRemoteTimestampRequest() { }

        public HttpMessageRemoteTimestampRequest(SyncContext context)
        {
            this.SyncContext = context ?? throw new ArgumentNullException(nameof(context));
        }

        [DataMember(Name = "sc", IsRequired = true, Order = 1)]
        public SyncContext SyncContext { get; set; }
    }

    [DataContract(Name = "summary"), Serializable]
    public class HttpMessageSummaryResponse : IScopeMessage
    {
        public HttpMessageSummaryResponse()
        {
        }

        public HttpMessageSummaryResponse(SyncContext context)
        {
            this.SyncContext = context ?? throw new ArgumentNullException(nameof(context));
        }

        [DataMember(Name = "bi", IsRequired = false, EmitDefaultValue = false, Order = 2)]
        public BatchInfo BatchInfo { get; set; }

        [DataMember(Name = "changes", IsRequired = false, EmitDefaultValue = false, Order = 5)]
        public ContainerSet Changes { get; set; }

        [DataMember(Name = "cca", IsRequired = false, EmitDefaultValue = false, Order = 7)]
        public DatabaseChangesApplied ClientChangesApplied { get; set; }

        [DataMember(Name = "crp", IsRequired = false, EmitDefaultValue = false, Order = 8)]
        public ConflictResolutionPolicy ConflictResolutionPolicy { get; set; }

        [DataMember(Name = "rct", IsRequired = false, Order = 3)]
        public long RemoteClientTimestamp { get; set; }

        [DataMember(Name = "scs", IsRequired = false, EmitDefaultValue = false, Order = 6)]
        public DatabaseChangesSelected ServerChangesSelected { get; set; }

        [DataMember(Name = "step", IsRequired = true, Order = 4)]
        public HttpStep Step { get; set; }

        [DataMember(Name = "sc", IsRequired = true, Order = 1)]
        public SyncContext SyncContext { get; set; }
    }

    [DataContract(Name = "endsessionreq"), Serializable]
    public class HttpMessageEndSessionRequest : IScopeMessage
    {
        public HttpMessageEndSessionRequest()
        {
        }

        public HttpMessageEndSessionRequest(SyncContext context)
        {
            this.SyncContext = context ?? throw new ArgumentNullException(nameof(context));
        }

        [DataMember(Name = "cac", IsRequired = false, Order = 5, EmitDefaultValue = false)]
        public DatabaseChangesApplied ChangesAppliedOnClient { get; set; }

        [DataMember(Name = "cas", IsRequired = false, Order = 4, EmitDefaultValue = false)]
        public DatabaseChangesApplied ChangesAppliedOnServer { get; set; }

        [DataMember(Name = "ccs", IsRequired = false, Order = 7, EmitDefaultValue = false)]
        public DatabaseChangesSelected ClientChangesSelected { get; set; }

        [DataMember(Name = "ct", IsRequired = true, Order = 3)]
        public DateTime CompleteTime { get; set; }

        [DataMember(Name = "scs", IsRequired = false, Order = 8, EmitDefaultValue = false)]
        public DatabaseChangesSelected ServerChangesSelected { get; set; }

        [DataMember(Name = "scac", IsRequired = false, Order = 6, EmitDefaultValue = false)]
        public DatabaseChangesApplied SnapshotChangesAppliedOnClient { get; set; }

        [DataMember(Name = "st", IsRequired = true, Order = 2)]
        public DateTime StartTime { get; set; }

        [DataMember(Name = "exc", IsRequired = false, Order = 9, EmitDefaultValue = false)]
        public string SyncExceptionMessage { get; set; }

        [DataMember(Name = "sc", IsRequired = true, Order = 1)]
        public SyncContext SyncContext { get; set; }
    }

    [DataContract(Name = "endsessionres"), Serializable]
    public class HttpMessageEndSessionResponse : IScopeMessage
    {
        public HttpMessageEndSessionResponse() { }

        public HttpMessageEndSessionResponse(SyncContext context)
        {
            this.SyncContext = context ?? throw new ArgumentNullException(nameof(context));
        }

        [DataMember(Name = "sc", IsRequired = true, Order = 1)]
        public SyncContext SyncContext { get; set; }
    }
}