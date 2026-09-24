using System.Numerics;

namespace RelicAtlas.UI;

public interface IAtlasArtwork
{
    bool Logo(Vector2 position, Vector2 bounds);
    bool Backdrop(Vector2 position, Vector2 size);
    bool Job(string job, Vector2 position, Vector2 size);
    bool Weapon(string name, Vector2 position, Vector2 size);
}
