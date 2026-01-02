
namespace Pandowdy.EmuCore.Interfaces
{
    public interface IVideoSubsystem
    {
        public RenderContext AllocateRenderContext();
        public void RenderFrame(RenderContext context);
    }
}
