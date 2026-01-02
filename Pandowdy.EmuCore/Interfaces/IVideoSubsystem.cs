namespace Pandowdy.EmuCore.Interfaces
{
    public interface IFrameGenerator
    {
        public RenderContext AllocateRenderContext();
        public void RenderFrame(RenderContext context);
    }
}
