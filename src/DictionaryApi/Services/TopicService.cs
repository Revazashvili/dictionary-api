using DictionaryApi.Entities;
using DictionaryApi.Models;
using DictionaryApi.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DictionaryApi.Services;

public class TopicService : ITopicService
{
    private readonly DictionaryDbContext _context;
    private readonly ITranslationService _translationService;

    public TopicService(DictionaryDbContext context,ITranslationService translationService)
    {
        _context = context;
        _translationService = translationService;
    }
    
    public async Task<IEnumerable<TopicDto>> GetAllAsync(CancellationToken cancellationToken)
    {
        var topics = await _context.Topics
            .Join(_context.Translations,
                topic => topic.TranslationId,
                translation => translation.TranslationId,
                (topic, translation) => new
                {
                    TopicId = topic.Id,
                    Translation = translation,
                })
            .GroupBy(result => result.TopicId)
            .Select(grouped => new TopicDto
            {
                Id = grouped.Key,
                NameTranslations = grouped.Select(a => new TranslationModel
                {
                    Language = a.Translation.Language,
                    Value = a.Translation.Value
                })
            })
            .ToListAsync(cancellationToken);

        return topics;
    }

    public async Task<TopicDto> GetByIdAsync(int id,CancellationToken cancellationToken)
    {
        var topic = await _context.Topics
            .SingleOrDefaultAsync(topic => topic.Id == id, cancellationToken);

        if (topic is null)
            throw new Exception("Topic not found");
        
        return new TopicDto
        {
            Id = topic.Id,
            NameTranslations = await _translationService.GetByTranslationIdAsync(topic.TranslationId, cancellationToken),
            SubTopics = new SubTopicDto[] { } //TODO: get from ISubTopicService
        };
    }

    public async Task<int> AddAsync(AddTopicRequest request,CancellationToken cancellationToken)
    {
        if (await _translationService.ExistsWithNamesAsync(request.NameTranslations, cancellationToken))
            throw new Exception("translation with names already exists");

        var translationId = Guid.NewGuid();
        await _translationService.AddAsync(translationId, request.NameTranslations, cancellationToken);

        var topic = new Topic(translationId);
        var entry = await _context.Topics.AddAsync(topic, cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        return entry.Entity.Id;
    }

    public async Task UpdateAsync(UpdateTopicRequest request,CancellationToken cancellationToken)
    {
        var topic = await _context.Topics
            .SingleOrDefaultAsync(topic => topic.Id == request.Id, cancellationToken);

        if (topic is null)
            throw new Exception("Topic not found");

        await _translationService.DeleteByTranslationIdAsync(topic.TranslationId, cancellationToken);
        await _translationService.AddAsync(topic.TranslationId, request.NameTranslations, cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(DeleteTopicRequest request,CancellationToken cancellationToken)
    {
        var topic = await _context.Topics
            .SingleOrDefaultAsync(topic => topic.Id == request.Id, cancellationToken);

        if (topic is null)
            throw new Exception("Topic not found");
        
        _context.Topics.Remove(topic);
        await _context.SaveChangesAsync(cancellationToken);
    }
}