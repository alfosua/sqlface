using SqlFace.Core.Schemas;

namespace SqlFace.Core;

public interface ISqlFaceContext<TTopic> : ISqlFaceContext { }

public interface ISqlFaceContext
{
    List<ISqlFaceSchemaDom> Schemas { get; set; }
}
