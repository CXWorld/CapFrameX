using CapFrameX.Webservice.Data.DTO;
using CapFrameX.Webservice.Data.Mappings;
using CapFrameX.Webservice.Data.Queries;
using CapFrameX.Webservice.Implementation.Services;
using MediatR;
using System.Threading;
using System.Threading.Tasks;

namespace CapFrameX.Webservice.Implementation.Handlers
{
    public class GetAppNotificationHandler: IRequestHandler<GetAppNotificationQuery, SqAppNotificationDataDTO>
    {
        private readonly SquidexService _squidexService;

        public GetAppNotificationHandler(SquidexService squidexService)
        {
            _squidexService = squidexService;
        }

        public async Task<SqAppNotificationDataDTO> Handle(GetAppNotificationQuery request, CancellationToken cancellationToken)
        {
            var notification = (await _squidexService.GetAppNotification())?.Data;
            return notification.ToAppNotificationDto();
        }
    }
}
